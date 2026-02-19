using Newtonsoft.Json.Linq;
using OWML.Common;
using OWML.Logging;
using OWML.ModHelper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

namespace ShipPlanetProjector
{
    public class PlanetDisplay : MonoBehaviour
    {

        Dictionary<string, GameObject> actualPlanets = new Dictionary<string, GameObject>();
        Dictionary<string, GameObject> planetModels = new Dictionary<string, GameObject>();

        InteractReceiver powerReciever;

        IModConsole modConsole;

        SandLevelController sandControllerET;
        SandLevelController sandControllerAT;
        SandLevelController lavaControllerHL;

        GameObject ashTwinSand;
        GameObject emberTwinSand;

        GameObject nameFocusTextObj;

        GameObject playerShipModel;
        Transform shipTransform;

        GameObject shipModelIndicator;
        Material shipModelIndicatorMat;
        bool isAnimatingShipIndicator = false;

        public List<MeteorLauncher> meteorLaunchControllers = new List<MeteorLauncher>();

        public List<GameObject> hologramMeteors = new List<GameObject>();
        public List<GameObject> actualMeteors = new List<GameObject>();
        public List<bool> hologramMeteorsImpacted = new List<bool>();

        bool displayPowered = false;

        string activePlanetName = "Timber Hearth";

        float rotationTicker = 0.0f;

        public static PlanetDisplay Create(Dictionary<string, GameObject> planets, Transform parent, IModConsole modHelperConsole)
        {
            var go = new GameObject("Planet Hologram Display");

            var planetController = go.AddComponent<PlanetDisplay>();
            planetController.planetModels = planets;
            planetController.modConsole = modHelperConsole;

            go.transform.position = parent.transform.position;
            go.transform.parent = parent;

            go.transform.localPosition = new Vector3(0.0f, 3.7f, 0.3f);

            return planetController;
        }

        public void Start()
        {
            shipTransform = Locator.GetShipTransform();
            var shipCockpit = shipTransform.Find("Module_Cockpit/Lights_Cockpit");

            var displayPowerSwitch = new GameObject("HoloMap Power");
            displayPowerSwitch.layer = LayerMask.NameToLayer("Interactible");

            var powerSwitchHitbox = displayPowerSwitch.AddComponent<SphereCollider>();
            powerSwitchHitbox.radius = 0.3f;
            powerSwitchHitbox.isTrigger = true;

            displayPowerSwitch.AddComponent<OWCollider>();
            powerReciever = displayPowerSwitch.AddComponent<InteractReceiver>();

            displayPowerSwitch.transform.parent = shipCockpit;
            displayPowerSwitch.transform.localPosition = new Vector3(-1.5f, 0.4f, 4.95f);

            powerReciever._usableInShip = true;
            powerReciever._interactRange = 2f;
            powerReciever.EnableInteraction();
            powerReciever.ChangePrompt("Toggle On HoloMap");
            powerReciever.OnPressInteract += ToggleDisplayPower;

            try
            {
                sandControllerET = Locator._hourglassTwinA.GetComponentInChildren<SandLevelController>();
                sandControllerAT = Locator._hourglassTwinB.GetComponentInChildren<SandLevelController>();
                lavaControllerHL = GameObject.Find("VolcanicMoon_Body/MoltenCore_VM").GetComponent<SandLevelController>();
            }
            catch
            {
                modConsole.WriteLine("Failed to locate sand controllers for Ember and Ash Twin", MessageType.Error);
            }

            try
            {
                actualPlanets["Hollow's Lantern"] = GameObject.Find("VolcanicMoon_Body");
                actualPlanets["The Attlerock"] = GameObject.Find("Moon_Body");
                actualPlanets["Timber Hearth"] = GameObject.Find("TimberHearth_Body");
                actualPlanets["Giant's Deep"] = GameObject.Find("GiantsDeep_Body");
                actualPlanets["Brittle Hollow"] = GameObject.Find("BrittleHollow_Body");
                actualPlanets["Ash Twin"] = GameObject.Find("TowerTwin_Body");
                actualPlanets["Ember Twin"] = GameObject.Find("CaveTwin_Body");
                actualPlanets["Twins"] = GameObject.Find("FocalBody");
                actualPlanets["Dark Bramble"] = GameObject.Find("DarkBramble_Body");
                actualPlanets["White Hole"] = GameObject.Find("WhiteHole_Body");
                actualPlanets["The Interloper"] = GameObject.Find("Comet_Body");
                actualPlanets["Quantum Moon"] = GameObject.Find("QuantumMoon_Body");

                actualPlanets["Orbital Probe Cannon"] = GameObject.Find("OrbitalProbeCannon_Body");
                actualPlanets["Orbital Probe Cannon Muzzle"] = GameObject.Find("CannonMuzzle_Body");
                actualPlanets["Orbital Probe Cannon Barrel"] = GameObject.Find("CannonBarrel_Body");

                actualPlanets["The Sun"] = GameObject.Find("Sun_Body");

                nameFocusTextObj = GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode/NamePanelRoot/Name");
            }
            catch
            {
                modConsole.WriteLine("Failed to locate planet bodies", MessageType.Error);
            }
            
            try
            {
                GameObject volcanicMoonEffects = GameObject.Find("VolcanicMoon_Body/Sector_VM/Effects_VM/");

                meteorLaunchControllers.Add(volcanicMoonEffects.transform.GetChild(1).GetChild(0).GetComponent<MeteorLauncher>());
                meteorLaunchControllers.Add(volcanicMoonEffects.transform.GetChild(2).GetChild(0).GetComponent<MeteorLauncher>());
                meteorLaunchControllers.Add(volcanicMoonEffects.transform.GetChild(3).GetChild(0).GetComponent<MeteorLauncher>());
                meteorLaunchControllers.Add(volcanicMoonEffects.transform.GetChild(4).GetChild(0).GetComponent<MeteorLauncher>());
            }
            catch
            {
                modConsole.WriteLine("Failed to locate meteor emitters", MessageType.Error);
            }

            try
            {
                CreateShipIndicator();
            }
            catch
            {
                modConsole.WriteLine("Failed to create the ship indicator", MessageType.Error);
            }

            foreach (KeyValuePair<string, GameObject> entry in planetModels)
            {
                GameObject planet = entry.Value;

                planet.transform.position = transform.position;
                planet.transform.parent = transform;

                Transform fogSphere = planet.transform.Find("ShockLayer");
                Transform planRoot = planet.transform.Find("Root");

                if (fogSphere == null && planRoot) fogSphere = planRoot.transform.Find("ShockLayer");

                if (fogSphere != null)
                {
                    float newScale = 1.0f / (fogSphere.localScale.x * 1.4f);
                    planet.transform.localScale = new Vector3(newScale, newScale, newScale);
                }
                else
                {
                    planet.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);
                }

                planet.transform.localPosition = new Vector3(-0.3f, 0.1f, 1.5f);
                planet.transform.localRotation = Quaternion.Euler(0.0f, 90.0f, 270.0f);
                

                if (entry.Key == "Giant's Deep") planet.transform.localScale = new Vector3(0.0007f, 0.0007f, 0.0007f);
                if (entry.Key == "Dark Bramble") planet.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
                if (entry.Key == "Brittle Hollow") planet.transform.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f);
                if (entry.Key == "The Sun") planet.transform.localScale = new Vector3(0.0002f, 0.0002f, 0.0002f);

                // Enable all the children of the object and remove hitboxes
                if (planRoot)
                {
                    planRoot.gameObject.SetActive(true);

                    foreach (Transform child in planRoot)
                    {
                        child.gameObject.SetActive(true);

                        SphereCollider sphereColl;
                        child.TryGetComponent<SphereCollider>(out sphereColl);
                        if (sphereColl) DestroyImmediate(sphereColl);

                        Collider coll;
                        child.TryGetComponent<Collider>(out coll);
                        if (coll) DestroyImmediate(coll);
                    }
                } else
                {
                    foreach (Transform child in planet.transform)
                    {
                        child.gameObject.SetActive(true);

                        SphereCollider sphereColl;
                        child.TryGetComponent<SphereCollider>(out sphereColl);
                        if (sphereColl) DestroyImmediate(sphereColl);

                        Collider coll;
                        child.TryGetComponent<Collider>(out coll);
                        if (coll) DestroyImmediate(coll);
                    }
                }

                if (entry.Key == "Twins")
                {
                    planet.transform.localRotation = Quaternion.Euler(0.0f, 90.0f, 270.0f);

                    Transform ashTwin = planet.transform.Find("AshTwin");
                    ashTwin.localRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);

                    Transform ashTwinRoot = ashTwin.Find("SandSphereRoot");
                    Material sandMat = ashTwinRoot.GetChild(0).GetComponent<TessellatedSphereRenderer>()._materials[0];

                    ashTwinSand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ashTwinSand.transform.parent = ashTwinRoot;
                    ashTwinSand.transform.localPosition = Vector3.zero;
                    ashTwinSand.GetComponent<SphereCollider>().enabled = false;
                    //ashTwinSand.GetComponent<Renderer>().material = sandMat;

                    float ashSandRadius = sandControllerAT.GetRadius() * 0.030303f;
                    ashTwinSand.transform.localScale = new Vector3(ashSandRadius, ashSandRadius, ashSandRadius);

                    Transform emberTwin = planet.transform.Find("EmberTwin");
                    emberTwin.localRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);

                    Transform emberTwinRoot = emberTwin.Find("SandSphereRoot");

                    emberTwinSand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    emberTwinSand.transform.parent = emberTwinRoot;
                    emberTwinSand.transform.localPosition = Vector3.zero;
                    emberTwinSand.GetComponent<SphereCollider>().enabled = false;
                    //emberTwinSand.GetComponent<Renderer>().material = sandMat;

                    float emberSandRadius = sandControllerET.GetRadius() * 0.030303f * 2.0f;
                    emberTwinSand.transform.localScale = new Vector3(emberSandRadius, emberSandRadius, emberSandRadius);
                }

                planet.SetActive(false);
            }

            if (planetModels["Brittle Hollow"])
            {
                GameObject brittleHollow = actualPlanets["Brittle Hollow"];
                GameObject whiteHole = actualPlanets["White Hole"];

                var realFragmentsForLookup = new List<DetachableFragment>(brittleHollow.GetComponentsInChildren<DetachableFragment>());

                int fragCount = 0;

                foreach (var fragment in planetModels["Brittle Hollow"].GetComponentsInChildren<ProxyBrittleHollowFragment>(true))
                {
                    var fragmentObj = fragment.gameObject.AddComponent<BrittleHollowFragmentManager>();
                    fragmentObj.Setup(fragment, planetModels["Brittle Hollow"], planetModels["White Hole"], modConsole);

                    if (brittleHollow != null && whiteHole != null)
                    {
                        fragmentObj.brittleHollowBody = brittleHollow;
                        fragmentObj.whiteHoleBody = whiteHole;
                    }

                    fragmentObj.SetRealFragment(realFragmentsForLookup.FirstOrDefault(
                        realFragment => fragment.realFragmentName.Equals(realFragment.gameObject.name)
                    ));

                    DestroyImmediate(fragment);
                    fragCount++;
                }

                modConsole.WriteLine($"Setup {fragCount} fragments", MessageType.Success);
            }

        }

        public void OnDestroy()
        {
            powerReciever.OnPressInteract -= ToggleDisplayPower;
        }

        private void DetectPlanetFocusChange()
        {
            if (planetModels != null)
            {
                // Get the focused planet
                string focusedPlanet = nameFocusTextObj.GetComponent<Text>().text;

                // Ember and Ash twins are grouped together
                if (focusedPlanet == "Ember Twin" || focusedPlanet == "Ash Twin") focusedPlanet = "Twins";

                // Moons still cause the focus to be on their parent
                if (focusedPlanet == "The Attlerock") focusedPlanet = "Timber Hearth";
                if (focusedPlanet == "Hollow's Lantern") focusedPlanet = "Brittle Hollow";

                // Orbital probe cannon still causes focus on Giant's Deep
                if (focusedPlanet == "Orbital Probe Cannon") focusedPlanet = "Giant's Deep";

                // Orbital probe cannon still causes focus on Giant's Deep
                if (focusedPlanet == "Sun Station") focusedPlanet = "The Sun";

                // Check that the focused planet is valid
                bool validPlanetName = false;

                if (focusedPlanet == "Twins") validPlanetName = true;
                if (focusedPlanet == "Timber Hearth") validPlanetName = true;
                if (focusedPlanet == "Brittle Hollow") validPlanetName = true;
                if (focusedPlanet == "Giant's Deep") validPlanetName = true;
                if (focusedPlanet == "Dark Bramble") validPlanetName = true;
                if (focusedPlanet == "White Hole") validPlanetName = true;
                if (focusedPlanet == "The Interloper") validPlanetName = true;
                if (focusedPlanet == "Quantum Moon") validPlanetName = true;
                if (focusedPlanet == "The Sun") validPlanetName = true;

                // Ignore invalid planets
                if (!validPlanetName) return;

                // Check that the planet is different to the last seleced planet
                if (focusedPlanet == activePlanetName) return;

                // If the previous active planet was the sun then reset all the planets positions
                if (activePlanetName == "The Sun") ResetModelPlanets();

                activePlanetName = focusedPlanet;

                // Switch the currently displayed planet
                SwitchDisplayedPlanet(activePlanetName);
            }
        }

        private void ResetModelPlanets()
        {
            foreach (KeyValuePair<string, GameObject> entry in planetModels)
            {
                GameObject planet = entry.Value;

                planet.transform.position = transform.position;
                planet.transform.parent = transform;

                Transform fogSphere = planet.transform.Find("ShockLayer");
                Transform planRoot = planet.transform.Find("Root");

                if (fogSphere == null && planRoot) fogSphere = planRoot.transform.Find("ShockLayer");

                if (fogSphere != null)
                {
                    float newScale = 1.0f / (fogSphere.localScale.x * 1.4f);
                    planet.transform.localScale = new Vector3(newScale, newScale, newScale);
                }
                else
                {
                    planet.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);
                }

                planet.transform.localPosition = new Vector3(-0.3f, 0.1f, 1.5f);
                planet.transform.localRotation = Quaternion.Euler(0.0f, 90.0f, 270.0f);


                if (entry.Key == "Giant's Deep") planet.transform.localScale = new Vector3(0.0007f, 0.0007f, 0.0007f);
                if (entry.Key == "Dark Bramble") planet.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
                if (entry.Key == "Brittle Hollow") planet.transform.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f);
                if (entry.Key == "The Sun") planet.transform.localScale = new Vector3(0.0002f, 0.0002f, 0.0002f);
            }
        }

        private void ToggleDisplayPower()
        {
            displayPowered = !displayPowered;

            if (displayPowered) {
                powerReciever.ChangePrompt("Toggle Off HoloMap");

                // Display the active planet
                SwitchDisplayedPlanet(activePlanetName);
            }
            else {
                powerReciever.ChangePrompt("Toggle On HoloMap");

                // Hide the displayed planet
                SwitchDisplayedPlanet("");
            }

            powerReciever.ResetInteraction();
        }

        private void SwitchDisplayedPlanet(string planetName)
        {
            foreach (KeyValuePair<string, GameObject> entry in planetModels)
            {
                GameObject planet = entry.Value;
                planet.SetActive(false);

                // If the current planet is selected then re-enable it
                if ((entry.Key == planetName || planetName == "The Sun") && displayPowered) planet.SetActive(true);

                // Disable the sun
                if (entry.Key == "The Sun") planet.transform.Find("Sun").gameObject.SetActive(false);

                // Disable both twins
                if (entry.Key == "Twins")
                {
                    planet.transform.Find("AshTwin").gameObject.SetActive(false);
                    planet.transform.Find("EmberTwin").gameObject.SetActive(false);
                }
            }

            // Enable the sun sphere if it is selected and powered
            if (planetName == "The Sun" && displayPowered) planetModels["The Sun"].transform.Find("Sun").gameObject.SetActive(true);

            // Enable both twins if they are selected and powered
            if ((planetName == "Twins" || planetName == "The Sun") && displayPowered)
            {
                planetModels["Twins"].transform.Find("AshTwin").gameObject.SetActive(true);
                planetModels["Twins"].transform.Find("EmberTwin").gameObject.SetActive(true);
            }
        }

        private void CheckToAddMeteors(List<MeteorController> meteorControllers)
        {
            //GameObject meteor = GameObject.Find("Prefab_VM_MoltenMeteor(Clone)");

            foreach (MeteorController meteorCont in meteorControllers)
            {

                GameObject meteor = meteorCont.transform.gameObject;

                if (meteor.transform.name.Contains("(used)")) continue;
                meteor.transform.name = meteor.transform.name + " (used)";

                GameObject fakeMeteor = null;
                Clone(ref fakeMeteor, meteor);

                DestroyImmediate(fakeMeteor.transform.Find("ConstantDetectors").gameObject);
                DestroyImmediate(fakeMeteor.transform.Find("DynamicDetector").gameObject);
                DestroyImmediate(fakeMeteor.transform.Find("MeteorImpactLight").gameObject);
                DestroyImmediate(fakeMeteor.transform.Find("MeteorShockLayer").gameObject);
                DestroyImmediate(fakeMeteor.transform.Find("ImpactAudioSource").gameObject);

                DestroyImmediate(fakeMeteor.transform.GetComponent<CenterOfTheUniverseOffsetApplier>());
                DestroyImmediate(fakeMeteor.transform.GetComponent<MeteorController>());
                DestroyImmediate(fakeMeteor.transform.GetComponent<OWRigidbody>());
                DestroyImmediate(fakeMeteor.transform.GetComponent<Rigidbody>());

                ShowGameObject(fakeMeteor);

                fakeMeteor.transform.Find("MeteorGlowLight").GetComponent<Light>().range = 150.0f * 0.0015f;
                fakeMeteor.transform.Find("MeteorGlowLight").GetComponent<Light>().intensity = 1.0f;

                Transform meteorBody = fakeMeteor.transform.Find("Meteor_Whole");
                if (meteorBody != null) meteorBody.gameObject.SetActive(true);

                fakeMeteor.transform.parent = planetModels["Brittle Hollow"].transform;

                hologramMeteors.Add(fakeMeteor);
                actualMeteors.Add(meteor);
                hologramMeteorsImpacted.Add(false);

            }
        }

        void Clone(ref GameObject field, GameObject toClone)
        {
            toClone.gameObject.SetActive(false);
            field = Instantiate(toClone.gameObject);
            if (field.TryGetComponent(out ProxyBody prox)) DestroyImmediate(prox);
            field.gameObject.SetActive(true);
            ShowGameObject(field);
            toClone.gameObject.SetActive(true);
        }

        void ShowGameObject(GameObject obj)
        {
            var proxies = obj.GetComponentsInChildren<Transform>(true);

            foreach (Transform proxy in proxies)
            {
                if (!proxy.TryGetComponent(out MeshRenderer rndr)) continue;
                rndr.enabled = true;
            }

            obj.SetActive(true);
        }

        private void UpdateMeteors()
        {
            for (int index = 0; index < hologramMeteors.Count; index++)
            {

                if (actualMeteors[index] == null)
                {
                    DestroyImmediate(hologramMeteors[index]);

                    string fixedName = actualMeteors[index].transform.name;
                    fixedName = fixedName.Substring(0, fixedName.Length - 7);
                    actualMeteors[index].transform.name = fixedName;

                    actualMeteors.RemoveAt(index);
                    hologramMeteors.RemoveAt(index);
                    hologramMeteorsImpacted.RemoveAt(index);
                    index--;
                    continue;
                }

                Transform meteorBody = hologramMeteors[index].transform.Find("Meteor_Whole");
                Transform meteorLight = hologramMeteors[index].transform.Find("MeteorGlowLight");

                if (!actualMeteors[index].transform.GetComponent<MeteorController>().hasImpacted)
                {
                    if (!meteorBody.gameObject.activeInHierarchy) meteorBody?.gameObject.SetActive(true);
                    if (!meteorLight.gameObject.activeInHierarchy) meteorLight?.gameObject.SetActive(true);
                    hologramMeteorsImpacted[index] = false;
                }

                // Check if effects are active
                if (actualMeteors[index].transform.GetComponent<MeteorController>().hasImpacted && !hologramMeteorsImpacted[index])
                {
                    hologramMeteorsImpacted[index] = true;

                    meteorBody?.gameObject.SetActive(false);
                    meteorLight?.gameObject.SetActive(false);

                    Transform particleHolder = hologramMeteors[index].transform.Find("Effects_VM_MeteorParticles");

                    List<ParticleSystem> meteorExplosionParticles = new List<ParticleSystem>(particleHolder.GetComponentsInChildren<ParticleSystem>());
                    foreach (ParticleSystem explosionParticles in meteorExplosionParticles)
                    {
                        explosionParticles.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);
                        explosionParticles.transform.gameObject.SetActive(true);

                        explosionParticles.Stop();
                        explosionParticles.time = 0.0f;
                        explosionParticles.Play();
                    }
                }

                hologramMeteors[index].transform.localPosition = actualPlanets["Brittle Hollow"].transform.InverseTransformPoint(actualMeteors[index].transform.position);
                hologramMeteors[index].transform.rotation = actualMeteors[index].transform.rotation;
                hologramMeteors[index].transform.localScale = Vector3.one;
            }
        }

        public void Update()
        {
            if (planetModels == null) return;

            // Check for planet focus change
            DetectPlanetFocusChange();

            if (!planetModels.ContainsKey(activePlanetName)) return;

            if (activePlanetName == "Timber Hearth" || activePlanetName == "The Sun")
            {
                planetModels["Timber Hearth"].transform.localRotation = Quaternion.Euler(rotationTicker * 0.57296557431f, 90.0f, 270.0f);

                var moonPivot = planetModels["Timber Hearth"].transform.Find("Moon_Pivot");
                if (moonPivot != null)
                {
                    moonPivot.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);

                    moonPivot.GetChild(0).localPosition = actualPlanets["Timber Hearth"].transform.InverseTransformPoint(actualPlanets["The Attlerock"].transform.position);
                    moonPivot.GetChild(0).localRotation = Quaternion.Inverse(actualPlanets["Timber Hearth"].transform.rotation) * actualPlanets["The Attlerock"].transform.rotation;
                }
            }

            if (activePlanetName == "Brittle Hollow" || activePlanetName == "The Sun")
            {
                planetModels["Brittle Hollow"].transform.localRotation = Quaternion.Euler(rotationTicker * 1.14594938724f, 90.0f, 270.0f);

                var moonPivot = planetModels["Brittle Hollow"].transform.Find("VolcanicMoon_Pivot");
                if (moonPivot != null)
                {
                    moonPivot.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);

                    moonPivot.GetChild(0).localPosition = actualPlanets["Brittle Hollow"].transform.InverseTransformPoint(actualPlanets["Hollow's Lantern"].transform.position);
                    moonPivot.GetChild(0).localRotation = Quaternion.Inverse(actualPlanets["Brittle Hollow"].transform.rotation) * actualPlanets["Hollow's Lantern"].transform.rotation;
                }

                foreach (MeteorLauncher launcher in meteorLaunchControllers)
                {
                    CheckToAddMeteors(launcher._launchedMeteors);
                }

                UpdateMeteors();
            }

            if (activePlanetName == "Giant's Deep" || activePlanetName == "The Sun")
            {
                Transform orbitalProbeCannonPivot = planetModels["Giant's Deep"].transform.Find("OrbitalProbeCannon_Pivot");
                if (orbitalProbeCannonPivot != null)
                {
                    //orbitalProbeCannonPivot.localRotation = Quaternion.Euler(0.0f, rotationTicker * 7.2f, 0.0f);

                    orbitalProbeCannonPivot.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);

                    // Backside
                    Transform probeCannonBackside = orbitalProbeCannonPivot.Find("OrbitalProbeCannon_Body");

                    if (probeCannonBackside != null)
                    {
                        probeCannonBackside.localPosition = actualPlanets["Giant's Deep"].transform.InverseTransformPoint(actualPlanets["Orbital Probe Cannon"].transform.position);
                        probeCannonBackside.localRotation = Quaternion.Inverse(actualPlanets["Giant's Deep"].transform.rotation) * actualPlanets["Orbital Probe Cannon"].transform.rotation;
                    }

                    // Barrel
                    Transform probeCannonBarrel = orbitalProbeCannonPivot.Find("CannonBarrel_Body");

                    if (probeCannonBarrel != null)
                    {
                        probeCannonBarrel.localPosition = actualPlanets["Giant's Deep"].transform.InverseTransformPoint(actualPlanets["Orbital Probe Cannon Barrel"].transform.position);
                        probeCannonBarrel.localRotation = Quaternion.Inverse(actualPlanets["Giant's Deep"].transform.rotation) * actualPlanets["Orbital Probe Cannon Barrel"].transform.rotation;
                    }

                    // Muzzle
                    Transform probeCannonMuzzle = orbitalProbeCannonPivot.Find("CannonMuzzle_Body");

                    if (probeCannonMuzzle != null)
                    {
                        probeCannonMuzzle.localPosition = actualPlanets["Giant's Deep"].transform.InverseTransformPoint(actualPlanets["Orbital Probe Cannon Muzzle"].transform.position);
                        probeCannonMuzzle.localRotation = Quaternion.Inverse(actualPlanets["Giant's Deep"].transform.rotation) * actualPlanets["Orbital Probe Cannon Muzzle"].transform.rotation;
                    }
                }
            }

            if (activePlanetName == "Twins" || activePlanetName == "The Sun")
            {
                //planetModels["Twins"].transform.localRotation = Quaternion.Euler(0.0f, 0.0f, rotationTicker * 6.54545454545f);

                Transform ashTwin = planetModels["Twins"].transform.Find("AshTwin");
                Transform emberTwin = planetModels["Twins"].transform.Find("EmberTwin");

                if (ashTwin != null && emberTwin != null)
                {
                    ashTwin.localPosition = actualPlanets["Twins"].transform.InverseTransformPoint(actualPlanets["Ash Twin"].transform.position);
                    ashTwin.localRotation = Quaternion.Inverse(actualPlanets["Twins"].transform.rotation) * actualPlanets["Ash Twin"].transform.rotation;

                    emberTwin.localPosition = actualPlanets["Twins"].transform.InverseTransformPoint(actualPlanets["Ember Twin"].transform.position);
                    emberTwin.localRotation = Quaternion.Inverse(actualPlanets["Twins"].transform.rotation) * actualPlanets["Ember Twin"].transform.rotation;

                    float ashSandRadius = sandControllerAT.GetRadius() * 0.030303f;
                    ashTwinSand.transform.localScale = new Vector3(ashSandRadius, ashSandRadius, ashSandRadius);

                    float emberSandRadius = sandControllerET.GetRadius() * 0.030303f * 2.0f;
                    emberTwinSand.transform.localScale = new Vector3(emberSandRadius, emberSandRadius, emberSandRadius);

                    Vector3 dir = emberTwin.transform.position - ashTwin.transform.position;
                    Quaternion lookRot = Quaternion.LookRotation(dir, ashTwin.transform.up);

                    Transform sandStreamFromAshTwin = ashTwin.transform.Find("SandColumnRoot");
                    GameObject sandColumnScaleRoot = GameObject.Find("SandFunnel_Body/ScaleRoot");

                    if (sandColumnScaleRoot != null && sandStreamFromAshTwin != null)
                    {
                        sandStreamFromAshTwin.localRotation = ashTwin.transform.InverseTransformRotation(lookRot);
                        sandStreamFromAshTwin.localScale = sandColumnScaleRoot.transform.localScale;
                    }
                }
            }

            if (activePlanetName == "The Sun")
            {
                // Sun              3000
                // Twins            
                // Timber Hearth    450   
                // Brittle Hollow   450
                // Giant's Deep     1100
                // Quantum Moon     150

                planetModels["Twins"].transform.parent = planetModels["The Sun"].transform;
                planetModels["Twins"].transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                planetModels["Twins"].transform.localPosition = actualPlanets["The Sun"].transform.InverseTransformPoint(actualPlanets["Twins"].transform.position);
                planetModels["Twins"].transform.localRotation = Quaternion.Inverse(actualPlanets["The Sun"].transform.rotation) * actualPlanets["Twins"].transform.rotation;

                planetModels["Timber Hearth"].transform.parent = planetModels["The Sun"].transform;
                planetModels["Timber Hearth"].transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                planetModels["Timber Hearth"].transform.localPosition = actualPlanets["The Sun"].transform.InverseTransformPoint(actualPlanets["Timber Hearth"].transform.position);
                planetModels["Timber Hearth"].transform.localRotation = Quaternion.Inverse(actualPlanets["The Sun"].transform.rotation) * actualPlanets["Timber Hearth"].transform.rotation;

                planetModels["Brittle Hollow"].transform.parent = planetModels["The Sun"].transform;
                planetModels["Brittle Hollow"].transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                planetModels["Brittle Hollow"].transform.localPosition = actualPlanets["The Sun"].transform.InverseTransformPoint(actualPlanets["Brittle Hollow"].transform.position);
                planetModels["Brittle Hollow"].transform.localRotation = Quaternion.Inverse(actualPlanets["The Sun"].transform.rotation) * actualPlanets["Brittle Hollow"].transform.rotation;

                planetModels["Giant's Deep"].transform.parent = planetModels["The Sun"].transform;
                planetModels["Giant's Deep"].transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                planetModels["Giant's Deep"].transform.localPosition = actualPlanets["The Sun"].transform.InverseTransformPoint(actualPlanets["Giant's Deep"].transform.position);
                planetModels["Giant's Deep"].transform.localRotation = Quaternion.Inverse(actualPlanets["The Sun"].transform.rotation) * actualPlanets["Giant's Deep"].transform.rotation;

                planetModels["Dark Bramble"].transform.parent = planetModels["The Sun"].transform;
                planetModels["Dark Bramble"].transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                planetModels["Dark Bramble"].transform.localPosition = actualPlanets["The Sun"].transform.InverseTransformPoint(actualPlanets["Dark Bramble"].transform.position);
                planetModels["Dark Bramble"].transform.localRotation = Quaternion.Inverse(actualPlanets["The Sun"].transform.rotation) * actualPlanets["Dark Bramble"].transform.rotation;

                planetModels["White Hole"].transform.parent = planetModels["The Sun"].transform;
                planetModels["White Hole"].transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                planetModels["White Hole"].transform.localPosition = actualPlanets["The Sun"].transform.InverseTransformPoint(actualPlanets["White Hole"].transform.position);
                planetModels["White Hole"].transform.localRotation = Quaternion.Inverse(actualPlanets["The Sun"].transform.rotation) * actualPlanets["White Hole"].transform.rotation;

                planetModels["The Interloper"].transform.parent = planetModels["The Sun"].transform;
                planetModels["The Interloper"].transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                planetModels["The Interloper"].transform.localPosition = actualPlanets["The Sun"].transform.InverseTransformPoint(actualPlanets["The Interloper"].transform.position);
                planetModels["The Interloper"].transform.localRotation = Quaternion.Inverse(actualPlanets["The Sun"].transform.rotation) * actualPlanets["The Interloper"].transform.rotation;

                // Check the comet is still alive
                if (!actualPlanets["The Interloper"].activeInHierarchy) planetModels["The Interloper"].SetActive(false);
                else planetModels["The Interloper"].SetActive(true);

                planetModels["Quantum Moon"].transform.parent = planetModels["The Sun"].transform;
                planetModels["Quantum Moon"].transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                planetModels["Quantum Moon"].transform.localPosition = actualPlanets["The Sun"].transform.InverseTransformPoint(actualPlanets["Quantum Moon"].transform.position);
                planetModels["Quantum Moon"].transform.localRotation = Quaternion.Inverse(actualPlanets["The Sun"].transform.rotation) * actualPlanets["Quantum Moon"].transform.rotation;
            }

            // Update the current local position of the ship relative to the planet on the display
            if (playerShipModel != null) SetShipIndicator(actualPlanets[activePlanetName]);

            // Run the pulse animation for the ship indicator
            if (!isAnimatingShipIndicator) StartCoroutine(AnimateShipIndicator());

            // Update the rotation of the current planet
            rotationTicker += Time.deltaTime;
        }

        private void CreateShipIndicator()
        {
            playerShipModel = new GameObject();

            playerShipModel.transform.parent = planetModels[activePlanetName].transform;
            playerShipModel.transform.localPosition = Vector3.zero;
            playerShipModel.transform.name = "Hologram Ship";

            shipModelIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shipModelIndicator.GetComponent<SphereCollider>().enabled = false;

            shipModelIndicator.transform.name = "Ship Indicator";

            // Create and set the material for the sphere
            shipModelIndicatorMat = new Material(Shader.Find("Standard"));
            shipModelIndicatorMat.EnableKeyword("_EMISSION");
            shipModelIndicatorMat.SetColor("_EmissionColor", new Color(1f, 0f, 0f) * 3.0f); // Red glow (no intensity multiplier)
            shipModelIndicator.GetComponent<MeshRenderer>().material = shipModelIndicatorMat;

            isAnimatingShipIndicator = false;

            // Load the mini ship model and clone it
            StartCoroutine(WaitToCloneModelMesh());
        }

        IEnumerator WaitToCloneModelMesh()
        {
            yield return new WaitForSeconds(2.0f);

            // Locate the tree template gameobject
            const string shipTemplatePath = "TimberHearth_Body/ModelRocket_Body/Geo_ModelRocket/Props_HEA_ModelRocket/";
            GameObject shipTemplate = GetGameObjectAtPath(shipTemplatePath);

            if (shipTemplate == null)
            {
                modConsole.WriteLine("Couldn't locate the ship template gameobject at: " + shipTemplatePath, MessageType.Error);
                yield break;
            }

            var objHandle = shipTemplate.GetComponent<StreamingMeshHandle>();
            if (objHandle) if (!string.IsNullOrEmpty(objHandle.assetBundle)) StreamingManager.LoadStreamingAssets(objHandle.assetBundle);

            foreach (var handle in shipTemplate.GetComponentsInChildren<StreamingMeshHandle>(true))
            {
                if (!string.IsNullOrEmpty(handle.assetBundle)) StreamingManager.LoadStreamingAssets(handle.assetBundle);
            }

            playerShipModel = Instantiate(shipTemplate);
            playerShipModel.transform.name = "Hologram Ship";

            // Parent the flashing indictor to the ship model and position it above the ship
            shipModelIndicator.transform.parent = playerShipModel.transform;
            shipModelIndicator.transform.localPosition = new Vector3(0.25f, 2.3988f, -1.1993f);
        }

        private void SetShipIndicator(GameObject planetBody)
        {
            playerShipModel.transform.parent = planetModels[activePlanetName].transform;

            // Update the ship's transform
            shipTransform = Locator.GetShipTransform();

            // Update the model ship's postion and rotation to match the actual ship's position and rotation relative to the selected planet
            playerShipModel.transform.localPosition = planetBody.transform.InverseTransformPoint(shipTransform.position);
            playerShipModel.transform.localRotation = planetBody.transform.InverseTransformRotation(shipTransform.rotation);
            playerShipModel.transform.localScale = new Vector3(6.0f, 6.0f, 6.0f);

            // Only display the ship if the holomap is powered
            if (displayPowered && planetBody.activeInHierarchy)
            {
                playerShipModel.SetActive(true);
                shipModelIndicator.SetActive(true);
            }
            else
            {
                playerShipModel.SetActive(false);
                shipModelIndicator.SetActive(false);
            }
        }

        private IEnumerator AnimateShipIndicator()
        {
            isAnimatingShipIndicator = true;

            float elapsedTime = 0.0f;

            float maxSize = 0.5f;
            float minSize = 0.2f;

            float duration = 0.5f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                // Get the current active planet and normalize scale
                float activePlanetScale = planetModels[activePlanetName].transform.localScale.x;
                float normalisedScale = 0.002f / activePlanetScale;

                // Expand the sphere
                shipModelIndicator.transform.localScale = Vector3.Lerp(Vector3.one * minSize * normalisedScale, Vector3.one * maxSize * normalisedScale, t);

                yield return null; // Wait for the next frame
            }

            elapsedTime = 0.0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                // Get the current active planet and normalize scale
                float activePlanetScale = planetModels[activePlanetName].transform.localScale.x;
                float normalisedScale = 0.002f / activePlanetScale;

                // Shrink the sphere
                shipModelIndicator.transform.localScale = Vector3.Lerp(Vector3.one * maxSize * normalisedScale, Vector3.one * minSize * normalisedScale, t);

                yield return null; // Wait for the next frame
            }

            // After the animation, reset and prepare for the next blink
            isAnimatingShipIndicator = false;
        }

        public GameObject GetGameObjectAtPath(string path)
        {
            string[] step_names = path.Split('/');

            GameObject go = GameObject.Find(step_names[0]);

            if (go == null)
            {
                modConsole.WriteLine($"Couldn't find object at path: {path}, failed to locate {step_names[0]}", MessageType.Error);
                return null;
            }

            for (int i = 1; i < step_names.Length - 1; i++)
            {
                Transform next_step = go.transform.Find(step_names[i]);

                if (next_step == null)
                {
                    modConsole.WriteLine($"Couldn't find object at path: {path}, failed to locate {step_names[i]}", MessageType.Error);
                    return null;
                }

                go = next_step.gameObject;
            }

            return go;
        }

    }
}
