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
        Dictionary<string, GameObject> planetModels = new Dictionary<string, GameObject>();
        Dictionary<string, GameObject> actualPlanets = new Dictionary<string, GameObject>();

        InteractReceiver powerReciever;

        IModConsole modConsole;

        SandLevelController sandControllerET;
        SandLevelController sandControllerAT;
        SandLevelController lavaControllerHL;

        GameObject ashTwinSand;
        GameObject emberTwinSand;

        GameObject mapModeObj;

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

        string activePlanetName = "TimberHearth";

        float rotationTicker = 0.0f;

        public static PlanetDisplay Create(Dictionary<string, GameObject> planetModels, Dictionary<string, GameObject> actualPlanets, Transform parent, IModConsole modHelperConsole)
        {
            var go = new GameObject("Planet Hologram Display");

            var planetController = go.AddComponent<PlanetDisplay>();
            planetController.planetModels = planetModels;
            planetController.actualPlanets = actualPlanets;
            planetController.modConsole = modHelperConsole;

            go.transform.position = parent.transform.position;
            go.transform.parent = parent;

            go.transform.localPosition = new Vector3(0.0f, 2.3f, 0.0f);
            go.transform.localRotation = Quaternion.identity;

            return planetController;
        }

        public void Start()
        {
            // Get the ship's cockpit module
            shipTransform = Locator.GetShipTransform();
            var shipCockpit = shipTransform.Find("Module_Cockpit/Lights_Cockpit");

            // Create the power switch for the hologram display
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

            try {

                // Try to locate the sand controllers for the twins
                sandControllerET = Locator._hourglassTwinA.GetComponentInChildren<SandLevelController>();
                sandControllerAT = Locator._hourglassTwinB.GetComponentInChildren<SandLevelController>();
                // Try to locate the lava controller for hollow's lantern
                lavaControllerHL = GameObject.Find("VolcanicMoon_Body/MoltenCore_VM").GetComponent<SandLevelController>();
            }
            catch
            {
                modConsole.WriteLine("Failed to locate sand controllers for Ember and Ash Twin", MessageType.Error);
            }

            try
            {
                actualPlanets["The Sun"] = GameObject.Find("Sun_Body");
            }
            catch
            {
                modConsole.WriteLine("Failed to locate planet bodies", MessageType.Error);
            }

            try
            {
                mapModeObj = GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode");
            }
            catch
            {
                modConsole.WriteLine("Failed to locate ship's log map", MessageType.Error);
            }

            try
            {
                // Try to get the volcanic moon meteor emitters
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

            // Create the ship indicator
            CreateShipIndicator();

            // Setup each planet model and parent them to the display
            foreach (KeyValuePair<string, GameObject> entry in planetModels)
            {
                GameObject planet = entry.Value;

                // Setup the planet model
                HoloDisplayUtils.SetupPlanet(planet, transform);

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

                // Disable the planet by default
                planet.SetActive(false);
            }

            if (planetModels.ContainsKey("BrittleHollow"))
            {
                GameObject brittleHollow = actualPlanets["BrittleHollow"];
                GameObject whiteHole = actualPlanets["WhiteHole"];

                var realFragmentsForLookup = new List<DetachableFragment>(brittleHollow.GetComponentsInChildren<DetachableFragment>());

                int fragCount = 0;

                foreach (var fragment in planetModels["BrittleHollow"].GetComponentsInChildren<ProxyBrittleHollowFragment>(true))
                {
                    var fragmentObj = fragment.gameObject.AddComponent<BrittleHollowFragmentManager>();
                    fragmentObj.Setup(fragment, planetModels["BrittleHollow"], planetModels["WhiteHole"], modConsole);

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

        public void UpdateSettings(bool atmospheresEnabled, bool cometTrailsEnabled)
        {

            if (planetModels.ContainsKey("Twins"))
            {
                // ET atmosphere
                Renderer atmopshereRendererAT = planetModels["Twins"].transform.Find("AshTwin")?.Find("Atmosphere_TowerTwin")?.Find("AtmoSphere")?.Find("Atmosphere_LOD3")?.GetComponent<MeshRenderer>();
                if (atmopshereRendererAT) atmopshereRendererAT.enabled = atmospheresEnabled;

                // AT atmosphere
                Renderer atmopshereRendererET = planetModels["Twins"].transform.Find("EmberTwin")?.Find("Atmosphere_CaveTwin")?.Find("AtmoSphere")?.Find("Atmosphere_LOD3")?.GetComponent<MeshRenderer>();
                if (atmopshereRendererET) atmopshereRendererET.enabled = atmospheresEnabled;
            }

            if (planetModels.ContainsKey("TimberHearth"))
            {
                // TH atmosphere
                Renderer atmopshereRendererTH = planetModels["TimberHearth"].transform.transform.Find("Atmosphere_TH")?.Find("Atmosphere_LOD3")?.GetComponent<MeshRenderer>();
                if (atmopshereRendererTH) atmopshereRendererTH.enabled = atmospheresEnabled;
            }

            if (planetModels.ContainsKey("BrittleHollow"))
            {
                // BH atmosphere
                Renderer atmopshereRendererBH = planetModels["BrittleHollow"].transform.transform.Find("Atmosphere_BH")?.Find("Atmosphere_LOD3")?.GetComponent<MeshRenderer>();
                if (atmopshereRendererBH) atmopshereRendererBH.enabled = atmospheresEnabled;
            }

            if (planetModels.ContainsKey("GiantsDeep"))
            {
                // GD atmosphere
                Renderer atmopshereRendererGD = planetModels["GiantsDeep"].transform.transform.Find("Atmosphere_GD")?.Find("Atmosphere_LOD3")?.GetComponent<MeshRenderer>();
                if (atmopshereRendererGD) atmopshereRendererGD.enabled = atmospheresEnabled;
            }

            if (planetModels.ContainsKey("Comet"))
            {
                // Get the Interloper tail holder
                Transform tailEffectsParent = planetModels["Comet"].transform.Find("Effects_CO")?.Find("Effects_CO_TailMeshes");

                // If it exists then loop through each tail and toggle the mesh renderer based on the comet trails setting
                if (tailEffectsParent)
                {
                    for (int i = 0; i < tailEffectsParent.childCount; i++)
                    {
                        Transform tail = tailEffectsParent.GetChild(i);
                        MeshRenderer tailMeshRenderer = tail.GetComponent<MeshRenderer>();

                        // Toggle the tail mesh renderer based on the comet trails setting
                        if (tailMeshRenderer) tailMeshRenderer.enabled = cometTrailsEnabled;
                    }
                }
            }

            modConsole.WriteLine($"Updated settings: atmospheresEnabled={atmospheresEnabled}, cometTrailsEnabled={cometTrailsEnabled}", MessageType.Success);
        }

        public void OnDestroy()
        {
            // Remove the interaction event to prevent errors if the display is destroyed
            powerReciever.OnPressInteract -= ToggleDisplayPower;
        }

        private void DetectPlanetFocusChange()
        {
            if (planetModels != null)
            {
                ShipLogMapMode shipLogMode = mapModeObj.GetComponent<ShipLogMapMode>();

                int selectedPlanetIndex = shipLogMode._objIndex;
                int selectedPlanetRow = shipLogMode._rowIndex;

                // Check that the selected indices are valid
                if (selectedPlanetIndex < 0) return;
                if (selectedPlanetRow < 0) return;

                if (selectedPlanetRow >= shipLogMode._astroObjects.Length) return;
                if (selectedPlanetIndex >= shipLogMode._astroObjects[selectedPlanetRow].Length) return;

                // Get the currently selected planet from the ship log
                ShipLogAstroObject currentLogPlanet = shipLogMode._astroObjects[selectedPlanetRow][selectedPlanetIndex];
                string focusedPlanet = currentLogPlanet.name;

                // Fix some of the names to match the planet models
                if (focusedPlanet == "CaveTwin" || focusedPlanet == "TowerTwin") focusedPlanet = "Twins";
                if (focusedPlanet == "Interloper") focusedPlanet = "Comet";
                if (focusedPlanet == "SunStation") focusedPlanet = "The Sun";

                // Check that the name exists in planet models
                if (!planetModels.ContainsKey(focusedPlanet)) return;

                // Check that the planet is different to the last seleced planet
                if (focusedPlanet == activePlanetName) return;

                // If the previous active planet was the sun then reset all the planets positions
                if (activePlanetName == "The Sun") ResetModelPlanets();

                // Set the active planet to the currently focused planet
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
                HoloDisplayUtils.SetupPlanet(planet, transform, false);
            }
        }

        private void ToggleDisplayPower()
        {
            displayPowered = !displayPowered;

            if (displayPowered) {
                powerReciever.ChangePrompt("Toggle Off HoloMap");
                SwitchDisplayedPlanet(activePlanetName); // Display the active planet
            }
            else {
                powerReciever.ChangePrompt("Toggle On HoloMap");
                SwitchDisplayedPlanet(""); // Hide the displayed planet
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
            }
        }

        private void CheckToAddMeteors(List<MeteorController> meteorControllers)
        {
            //GameObject meteor = GameObject.Find("Prefab_VM_MoltenMeteor(Clone)");

            foreach (MeteorController meteorCont in meteorControllers)
            {

                GameObject meteor = meteorCont.transform.gameObject;

                // Check if the meteor is already being tracked
                if (meteor.transform.name.Contains("(used)")) continue;
                meteor.transform.name = meteor.transform.name + " (used)";

                // Clone the meteor and remove all the unnecessary components
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

                fakeMeteor.transform.Find("MeteorGlowLight")?.GetComponent<Light>().range = 150.0f * 0.0015f;
                fakeMeteor.transform.Find("MeteorGlowLight")?.GetComponent<Light>().intensity = 1.0f;

                fakeMeteor.transform.Find("Meteor_Whole")?.gameObject.SetActive(true);

                fakeMeteor.transform.parent = planetModels["BrittleHollow"].transform;

                hologramMeteors.Add(fakeMeteor);
                actualMeteors.Add(meteor);
                hologramMeteorsImpacted.Add(false);
            }
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

                MeteorController meteorController = actualMeteors[index].transform.GetComponent<MeteorController>();

                Transform meteorBody = hologramMeteors[index].transform.Find("Meteor_Whole");
                Transform meteorLight = hologramMeteors[index].transform.Find("MeteorGlowLight");

                if (!meteorController.hasImpacted)
                {
                    if (!meteorBody.gameObject.activeInHierarchy) meteorBody.gameObject.SetActive(true);
                    if (!meteorLight.gameObject.activeInHierarchy) meteorLight.gameObject.SetActive(true);
                    hologramMeteorsImpacted[index] = false;
                }

                // Check if effects are active
                if (meteorController.hasImpacted && !hologramMeteorsImpacted[index])
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

                // Update the hologram meteor's position and rotation to match the actual meteor
                hologramMeteors[index].transform.localPosition = actualPlanets["BrittleHollow"].transform.InverseTransformPoint(actualMeteors[index].transform.position);
                hologramMeteors[index].transform.localRotation = Quaternion.Inverse(actualPlanets["BrittleHollow"].transform.rotation) * actualMeteors[index].transform.rotation;
                hologramMeteors[index].transform.localScale = Vector3.one;
            }
        }

        public void Update()
        {
            if (planetModels == null) return;

            // Check for planet focus change
            DetectPlanetFocusChange();

            // Make sure the active planet exists
            if (!planetModels.ContainsKey(activePlanetName)) return;

            // Update all planets moons positions
            foreach (KeyValuePair<string, GameObject> entry in planetModels)
            {
                GameObject planet = entry.Value;
                string planetName = entry.Key;

                // Skip the sun
                if (planetName == "The Sun") continue;

                // Check that the planet is actually a satellite
                if (planet.transform.parent)
                {
                    if (planet.transform.parent.name.Contains("_Pivot")) continue;
                }

                if (actualPlanets.ContainsKey("The Sun")) HoloDisplayUtils.SetHologramRotation(planet, actualPlanets[planetName], actualPlanets["The Sun"]);
                
                // Check the planet's children for moon pivots
                foreach (Transform child in planet.transform)
                {
                    if (child.name.Contains("_Pivot"))
                    {
                        child.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);

                        // Loop through all the children of the pivot and update their positions and rotations
                        foreach (Transform satellite in child.transform)
                        {
                            HoloDisplayUtils.SetHologramPositionAndRotation(satellite.gameObject, actualPlanets[satellite.name], actualPlanets[planetName]);
                        }
                    }
                }
            }

            // Check for new meteors to add to the display
            foreach (MeteorLauncher launcher in meteorLaunchControllers)
            {
                CheckToAddMeteors(launcher._launchedMeteors);
            }

            // Update meteor positions
            UpdateMeteors();

            if (activePlanetName == "Twins" || activePlanetName == "The Sun")
            {
                Transform ashTwin = planetModels["Twins"].transform.Find("AshTwin");
                Transform emberTwin = planetModels["Twins"].transform.Find("EmberTwin");

                if (ashTwin != null && emberTwin != null)
                {
                    HoloDisplayUtils.SetHologramPositionAndRotation(ashTwin.gameObject, actualPlanets["AshTwin"], actualPlanets["Twins"]);
                    HoloDisplayUtils.SetHologramPositionAndRotation(emberTwin.gameObject, actualPlanets["EmberTwin"], actualPlanets["Twins"]);

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

                // Loop through each planet model and update its position and rotation to
                // match the actual planet's position and rotation relative to the sun
                foreach (KeyValuePair<string, GameObject> entry in planetModels)
                {
                    GameObject planet = entry.Value;
                    string planetName = entry.Key;

                    // Skip the sun
                    if (planetName == "The Sun") continue;

                    planet.transform.parent = planetModels["The Sun"].transform;
                    planet.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

                    HoloDisplayUtils.SetHologramPositionAndRotation(planet, actualPlanets[planetName], actualPlanets["The Sun"]);
                }

                // Check the comet is still alive
                if (!actualPlanets["Comet"].activeInHierarchy) planetModels["Comet"].SetActive(false);
                else planetModels["Comet"].SetActive(true);
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
            playerShipModel.transform.localScale = new Vector3(6.0f, 6.0f, 6.0f);
            HoloDisplayUtils.SetHologramPositionAndRotation(playerShipModel, shipTransform.gameObject, planetBody);

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

                // Wait for the next frame
                yield return null;
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

                // Wait for the next frame
                yield return null;
            }

            // After the animation, reset and prepare for the next blink
            isAnimatingShipIndicator = false;
        }

        public GameObject GetGameObjectAtPath(string path)
        {
            string[] step_names = path.Split('/');

            // Get the first step in the path's corresponding GameObject
            GameObject go = GameObject.Find(step_names[0]);

            // If the first step doesn't exist then return null
            if (go == null)
            {
                modConsole.WriteLine($"Couldn't find object at path: {path}, failed to locate {step_names[0]}", MessageType.Error);
                return null;
            }

            // Iterate through the remaining steps in the path and find the corresponding child GameObject at each step
            for (int i = 1; i < step_names.Length - 1; i++)
            {
                Transform next_step = go.transform.Find(step_names[i]);

                // If the next step doesn't exist then return null
                if (next_step == null)
                {
                    modConsole.WriteLine($"Couldn't find object at path: {path}, failed to locate {step_names[i]}", MessageType.Error);
                    return null;
                }

                // Update the current GameObject to the next step in the path
                go = next_step.gameObject;
            }

            // Return the final GameObject
            return go;
        }

        // Credit to MegaPiggy for the cloning method, which allows us to clone the proxy bodies so they
        // can be used as holograms in the ship.
        // The Clone and showProxy functons are from AdvancedMinimap.cs file in the General Enhancements mod:
        // https://github.com/MegaPiggy/SBtT.GeneralEnhancements/
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

    }
}
