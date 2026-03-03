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
using UnityEngine.InputSystem.HID;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

namespace ShipPlanetProjector
{
    public class PlanetDisplay : MonoBehaviour
    {
        private Dictionary<string, GameObject> planetModels = new Dictionary<string, GameObject>();
        private Dictionary<string, GameObject> actualPlanets = new Dictionary<string, GameObject>();

        private GameObject fragmentHolder;
        private GameObject meteorHolder;

        private InteractReceiver powerReciever;

        private IModConsole modConsole;

        private GameObject mapModeObj;

        private GameObject playerShipModel;
        private Transform shipTransform;

        private List<string> assetBundles = new List<string>();

        private GameObject shipModelIndicator;
        private Material shipModelIndicatorMat;
        private bool isAnimatingShipIndicator = false;

        private bool displayPowered = false;

        private string activePlanetName = "TimberHearth";
        private static string SAFE_PLANET_NAME = "";

        // The maximum distance from the currently focused planet another object can be
        // before it is hidden (only allow planets inside the ship cabin to be visible)
        private const float DISPLAY_CUTOFF_DIST = 3.0f;

        public static PlanetDisplay Create(Dictionary<string, GameObject> planetModels, Dictionary<string, GameObject> actualPlanets, Transform parent, IModConsole modHelperConsole)
        {
            var go = new GameObject("Planet Hologram Display");

            var planetController = go.AddComponent<PlanetDisplay>();
            planetController.planetModels = planetModels;
            planetController.actualPlanets = actualPlanets;
            planetController.modConsole = modHelperConsole;

            planetController.fragmentHolder = new GameObject("Fragment Holder");
            planetController.fragmentHolder.transform.parent = go.transform;
            planetController.fragmentHolder.transform.localPosition = Vector3.zero;
            planetController.fragmentHolder.transform.rotation = Quaternion.identity;
            planetController.fragmentHolder.transform.localScale = Vector3.one;

            planetController.meteorHolder = new GameObject("Meteor Holder");
            planetController.meteorHolder.transform.parent = go.transform;
            planetController.meteorHolder.transform.localPosition = Vector3.zero;
            planetController.meteorHolder.transform.rotation = Quaternion.identity;
            planetController.meteorHolder.transform.localScale = Vector3.one;

            go.transform.position = parent.transform.position;
            go.transform.parent = parent;

            go.transform.localPosition = new Vector3(0.0f, 2.3f, 0.0f);
            go.transform.localRotation = Quaternion.identity;

            foreach (KeyValuePair<string, GameObject> entry in planetModels)
            {
                SAFE_PLANET_NAME = entry.Key;
                if (SAFE_PLANET_NAME == "TimberHearth") break;
            }

            // Set the scale of the display based on the current focused planet
            HoloDisplayUtils.SetDisplayScale(go, planetModels[SAFE_PLANET_NAME], 0.002f);

            // Setup the Brittle Hollow fragments
            ProjectorFragmentManager.SetProjectorCutOffDistance(DISPLAY_CUTOFF_DIST);
            ProjectorFragmentManager.SetDisplayPower(false);
            ProjectorFragmentManager.SetFocusedPlanet(planetModels[SAFE_PLANET_NAME], actualPlanets[SAFE_PLANET_NAME], 0.002f);
            // Setup the meteor manager
            ProjectorMeteorManager.SetProjectorCutOffDistance(DISPLAY_CUTOFF_DIST);
            ProjectorMeteorManager.SetDisplayPower(false);
            ProjectorMeteorManager.SetFocusedPlanet(planetModels[SAFE_PLANET_NAME], actualPlanets[SAFE_PLANET_NAME], 0.002f);

            return planetController;
        }

        public void Start()
        {
            // Make sure the active planet exists in planet models
            activePlanetName = SAFE_PLANET_NAME;

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

            /*try
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
            }*/

            // Create the ship indicator
            CreateShipIndicator();

            // Set the mod console in HoloDisplayUtils
            HoloDisplayUtils.modConsole = modConsole;

            // Setup each planet model and parent them to the display
            foreach (KeyValuePair<string, GameObject> entry in planetModels)
            {
                GameObject planet = entry.Value;

                // Setup the planet model
                HoloDisplayUtils.SetupPlanet(planet, transform.gameObject, fragmentHolder, transform.localScale.x);

                if (entry.Key == "AshTwin")
                {
                    Transform ashTwinRoot = planetModels["AshTwin"].transform.Find("SandSphereRoot");
                    Material sandMat = Instantiate(ashTwinRoot.GetChild(0).GetComponent<TessellatedSphereRenderer>()._materials[0]);

                    GameObject ashTwinSand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ashTwinSand.name = "AshTwin Sand";
                    ashTwinSand.transform.parent = planet.transform;
                    ashTwinSand.transform.localPosition = Vector3.zero;
                    ashTwinSand.GetComponent<SphereCollider>().enabled = false;
                    //ashTwinSand.GetComponent<Renderer>().material = sandMat;

                    SandLevelController sandControllerAT = actualPlanets["AshTwin"].transform.GetComponentInChildren<SandLevelController>();
                    //float ashSandRadius = sandControllerAT.GetRadius() * 0.030303f;
                    ashTwinSand.transform.localScale = Vector3.one * sandControllerAT.GetRadius() * 2.060606f;
                }

                if (entry.Key == "EmberTwin")
                {
                    Transform emberTwinRoot = planetModels["EmberTwin"].transform.Find("SandSphereRoot");
                    Material sandMat = Instantiate(emberTwinRoot.GetChild(0).GetComponent<TessellatedSphereRenderer>()._materials[0]);

                    GameObject emberTwinSand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    emberTwinSand.name = "EmberTwin Sand";
                    emberTwinSand.transform.parent = planet.transform;
                    emberTwinSand.transform.localPosition = Vector3.zero;
                    emberTwinSand.GetComponent<SphereCollider>().enabled = false;
                    //emberTwinSand.GetComponent<Renderer>().material = sandMat;

                    SandLevelController sandControllerET = actualPlanets["AshTwin"].transform.GetComponentInChildren<SandLevelController>();
                    emberTwinSand.transform.localScale = Vector3.one * sandControllerET.GetRadius() * 2.060606f;
                }

                // Disable the planet by default
                planet.SetActive(false);
            }

            // Locate any meteor emitted in the actual planets
            foreach (KeyValuePair<string, GameObject> entry in actualPlanets)
            {
                if (!actualPlanets[entry.Key]) continue;

                foreach (MeteorLauncher meteorLauncher in HoloDisplayUtils.FindMeteorLaunchers(actualPlanets[entry.Key]))
                {
                    var meteorController = meteorLauncher.transform.gameObject.AddComponent<ProjectorMeteorManager>();
                    meteorController.Setup(meteorLauncher, meteorHolder, modConsole);
                }
            }

        }

        public void UpdateSettings(bool atmospheresEnabled, bool cometTrailsEnabled)
        {
            // Update the planet settings
            HoloDisplayUtils.UpdatePlanetSettings(gameObject, atmospheresEnabled, cometTrailsEnabled);

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

                // Format (for NH)
                focusedPlanet = focusedPlanet.Replace("_ShipLog", "");

                // Fix some of the names to match the planet models
                if (focusedPlanet == "CaveTwin") focusedPlanet = "EmberTwin";
                if (focusedPlanet == "TowerTwin") focusedPlanet = "AshTwin";

                if (planetModels.ContainsKey("Twins")) {
                    if (focusedPlanet == "EmberTwin" || focusedPlanet == "AshTwin") focusedPlanet = "Twins";
                }
                
                if (focusedPlanet == "Interloper") focusedPlanet = "Comet";
                if (focusedPlanet == "SunStation") focusedPlanet = "The Sun";

                // Check that the name exists in planet models
                if (!planetModels.ContainsKey(focusedPlanet)) return;

                // Check that the planet is different to the last seleced planet
                if (focusedPlanet == activePlanetName) return;

                // Set the active planet to the currently focused planet
                activePlanetName = focusedPlanet;

                // Set the scale of the display based on the current focused planet
                HoloDisplayUtils.SetDisplayScale(transform.gameObject, planetModels[activePlanetName], transform.localScale.x);

                // Update the Brittle Hollow fragments
                ProjectorFragmentManager.SetFocusedPlanet(planetModels[activePlanetName], actualPlanets[activePlanetName], transform.localScale.x);
                // Update the meteors
                ProjectorMeteorManager.SetFocusedPlanet(planetModels[activePlanetName], actualPlanets[activePlanetName], transform.localScale.x);
            }
        }

        private void ToggleDisplayPower()
        {
            displayPowered = !displayPowered;

            // Update the fragement and meteor managers
            ProjectorFragmentManager.SetDisplayPower(displayPowered);
            ProjectorMeteorManager.SetDisplayPower(displayPowered);

            if (displayPowered) {
                powerReciever.ChangePrompt("Toggle Off HoloMap");
            }
            else {
                powerReciever.ChangePrompt("Toggle On HoloMap");
            }

            foreach (KeyValuePair<string, GameObject> entry in planetModels)
            {
                GameObject planet = entry.Value;
                planet.SetActive(displayPowered);
            }

            powerReciever.ResetInteraction();
        }

        public void Update()
        {
            if (planetModels == null) return;

            /*
             * Rather than hiding non-active planets, hide planets which are too far from the ship
             * and position all planets relative to the currently selected planet, fixes an issue
             * where moon's may not be children of the planet proxy bodies
             */

            // Sun              3000
            // Twins            
            // Timber Hearth    450   
            // Brittle Hollow   450
            // Giant's Deep     1100
            // Quantum Moon     150

            // Check for planet focus change
            DetectPlanetFocusChange();

            // Make sure the active planet exists
            if (!planetModels.ContainsKey(activePlanetName)) return;

            // Update all planets moons positions
            foreach (KeyValuePair<string, GameObject> entry in planetModels)
            {
                GameObject planet = entry.Value;
                string planetName = entry.Key;

                HoloDisplayUtils planetHDU = planet.GetComponent<HoloDisplayUtils>();

                // Skip parented moons as they are postioned by their parent planet
                if (planetHDU.hasParentPlanet) continue;

                if (planet.name == activePlanetName)
                {
                    planet.transform.localPosition = Vector3.zero;
                    planet.transform.localRotation = Quaternion.identity;
                    //if (actualPlanets.ContainsKey("The Sun")) HoloDisplayUtils.SetHologramRotation(planet, actualPlanets[planetName], actualPlanets["The Sun"]);
                }

                // Position non-focues planets relative to the focused planet
                if (planet.name != activePlanetName)
                {
                    HoloDisplayUtils.SetHologramPositionAndRotation(planet, actualPlanets[planetName], actualPlanets[activePlanetName]);
                }

                // Update any of the planet's parented moons (non-parented moons are positioned seperately
                foreach (GameObject moon in planetHDU.moons)
                {
                    moon.transform.parent.localRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);
                    HoloDisplayUtils.SetHologramPositionAndRotation(moon, actualPlanets[moon.name], actualPlanets[planetName]);
                }

                if (planet.name == "AshTwin")
                {
                    SandLevelController sandControllerAT = actualPlanets["AshTwin"].transform.GetComponentInChildren<SandLevelController>();
                    GameObject ashTwinSand = planetModels["AshTwin"].transform.Find("AshTwin Sand").gameObject;

                    ashTwinSand.transform.localScale = Vector3.one * sandControllerAT.GetRadius() * 2.060606f;

                    // Make the sand stream point towards the other twin
                    Vector3 dir = planetModels["EmberTwin"].transform.position - planetModels["AshTwin"].transform.position;
                    Quaternion lookRot = Quaternion.LookRotation(dir, planetModels["AshTwin"].transform.up);

                    Transform sandStreamFromAshTwin = planetModels["AshTwin"].transform.Find("SandColumnRoot");
                    GameObject sandColumnScaleRoot = GameObject.Find("SandFunnel_Body/ScaleRoot");

                    if (sandColumnScaleRoot != null && sandStreamFromAshTwin != null)
                    {
                        sandStreamFromAshTwin.localRotation = planetModels["AshTwin"].transform.InverseTransformRotation(lookRot);
                        sandStreamFromAshTwin.localScale = sandColumnScaleRoot.transform.localScale;
                    }
                }

                if (planet.name == "EmberTwin")
                {
                    SandLevelController sandControllerET = actualPlanets["EmberTwin"].transform.GetComponentInChildren<SandLevelController>();
                    GameObject emberTwinSand = planetModels["EmberTwin"].transform.Find("EmberTwin Sand").gameObject;

                    emberTwinSand.transform.localScale = Vector3.one * sandControllerET.GetRadius() * 2.060606f;
                }

                // Hide planets which are outside the ship's cabin
                float mag = planet.transform.localPosition.magnitude * transform.localScale.x;
                planet.SetActive(mag <= DISPLAY_CUTOFF_DIST && displayPowered ? true : false);
            }

            // Update the current local position of the ship relative to the planet on the display
            if (playerShipModel != null) SetShipIndicator(actualPlanets[activePlanetName]);

            // Run the pulse animation for the ship indicator
            if (!isAnimatingShipIndicator) StartCoroutine(AnimateShipIndicator());
        }

        private void CreateShipIndicator()
        {
            playerShipModel = new GameObject();

            playerShipModel.transform.parent = transform;
            playerShipModel.transform.localPosition = Vector3.zero;
            playerShipModel.transform.name = "Hologram Ship";

            shipModelIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shipModelIndicator.GetComponent<SphereCollider>().enabled = false;

            shipModelIndicator.transform.name = "Ship Indicator";

            // Create and set the material for the sphere
            shipModelIndicatorMat = new Material(Shader.Find("Standard"));
            shipModelIndicatorMat.EnableKeyword("_EMISSION");
            shipModelIndicatorMat.SetColor("_EmissionColor", new Color(1f, 0f, 0f) * 3.0f); // Red glow
            shipModelIndicator.GetComponent<MeshRenderer>().material = shipModelIndicatorMat;

            shipModelIndicator.transform.parent = playerShipModel.transform;
            shipModelIndicator.transform.localPosition = Vector3.zero;

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
                if (!string.IsNullOrEmpty(handle.assetBundle))
                {
                    StreamingManager.LoadStreamingAssets(handle.assetBundle);
                    assetBundles.Add(handle.assetBundle);
                }
            }

            Sector timberHearthSector = Locator.GetAstroObject(AstroObject.Name.TimberHearth).GetComponentInChildren<Sector>();

            timberHearthSector.OnOccupantEnterSector += OnEnterTimberHearth;
            timberHearthSector.OnOccupantExitSector += OnExitTimberHearth;

            playerShipModel = Instantiate(shipTemplate);
            playerShipModel.transform.name = "Hologram Ship";
            playerShipModel.transform.SetParent(transform, false);

            // Parent the flashing indictor to the ship model and position it above the ship
            shipModelIndicator.transform.parent = playerShipModel.transform;
            shipModelIndicator.transform.localPosition = new Vector3(0.25f, 2.3988f, -1.1993f);
        }

        private void OnEnterTimberHearth(SectorDetector detector)
        {
            // Load all the asset bundles
            foreach (string bundle in assetBundles) StreamingManager.LoadStreamingAssets(bundle);
        }

        private void OnExitTimberHearth(SectorDetector detector)
        {
            // Load all the asset bundles
            foreach (string bundle in assetBundles) StreamingManager.LoadStreamingAssets(bundle);
        }

        private void SetShipIndicator(GameObject planetBody)
        {
            // Update the ship's transform
            shipTransform = Locator.GetShipTransform();

            // Update the model ship's postion and rotation to match the actual ship's position and rotation relative to the selected planet
            playerShipModel.transform.localScale = (Vector3.one * 6.0f * 0.002f) / transform.localScale.x;
            HoloDisplayUtils.SetHologramPositionAndRotation(playerShipModel, shipTransform.gameObject, planetBody);

            // Only display the ship if the holomap is powered
            if (displayPowered)
            {
                playerShipModel.SetActive(true);
                shipModelIndicator.SetActive(true);
            }
            else
            {
                playerShipModel.SetActive(false);
                shipModelIndicator.SetActive(false);
            }

            // Hide the ship indicator if it is outside the ship's cabin
            float mag = playerShipModel.transform.localPosition.magnitude * transform.localScale.x;
            playerShipModel.SetActive(mag <= DISPLAY_CUTOFF_DIST && displayPowered ? true : false);
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

                // Expand the sphere
                shipModelIndicator.transform.localScale = Vector3.Lerp(Vector3.one * minSize, Vector3.one * maxSize, t);

                // Wait for the next frame
                yield return null;
            }

            elapsedTime = 0.0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                // Shrink the sphere
                shipModelIndicator.transform.localScale = Vector3.Lerp(Vector3.one * maxSize, Vector3.one * minSize, t);

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
