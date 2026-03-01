using Epic.OnlineServices;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace ShipPlanetProjector
{
    public class ShipPlanetProjector : ModBehaviour
    {
        public static ShipPlanetProjector Instance;

        private static readonly Dictionary<string, GameObject> CachedGameObjects = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, GameObject> CachedRootGameObjects = new Dictionary<string, GameObject>();

        PlanetDisplay planetDisplay;

        Dictionary<string, GameObject> planetModels = new Dictionary<string, GameObject>();
        Dictionary<string, GameObject> actualPlanets = new Dictionary<string, GameObject>();

        public void Awake()
        {
            Instance = this;
            // You won't be able to access OWML's mod helper in Awake.
            // So you probably don't want to do anything here.
            // Use Start() instead.
        }

        public void Start()
        {
            // Starting here, you'll have access to OWML's mod helper.
            ModHelper.Console.WriteLine($"{nameof(ShipPlanetProjector)} has loaded!", MessageType.Success);

            new Harmony("GameDev46.ShipPlanetProjector").PatchAll(Assembly.GetExecutingAssembly());

            //OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen); // We start on title screen
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (newScene != OWScene.SolarSystem) return;

            StartCoroutine(waitToSetupPlanets());
        }

        // Called by OWML; once at the start and upon each config setting change.
        public override void Configure(IModConfig config)
        {
            if (planetDisplay == null) return;

            bool atmospheresEnabled = config.GetSettingsValue<string>("planetAtmospheres") == "Enabled";
            bool cometTrailsEnabled = config.GetSettingsValue<string>("interloperEffects") == "Enabled";

            planetDisplay.UpdateSettings(atmospheresEnabled, cometTrailsEnabled);
        }

        IEnumerator waitToSetupPlanets()
        {
            yield return new WaitForSeconds(2.0f);
            SetupPlanetProjector();
        }

        private void SetupPlanetProjector()
        {
            Transform shipTransform = Locator.GetShipTransform();
            Transform shipCabinTransform = shipTransform.Find("Module_Cabin");

            if (shipTransform == null)
            {
                ModHelper.Console.WriteLine("Couldn't locate the ship's transform", MessageType.Error);
                return;
            }

            if (shipCabinTransform == null)
            {
                ModHelper.Console.WriteLine("Couldn't locate the ship's cabin module", MessageType.Error);
                return;
            }

            // Find all the proxy body objects
            GameObject proxyManager = GameObject.Find("DistantProxyManager");
            var distanceProxies = proxyManager.GetComponent<DistantProxyManager>()._proxies;

            // Clear the planet models and actual planets dictionaries
            planetModels = new Dictionary<string, GameObject>();
            actualPlanets = new Dictionary<string, GameObject>();

            // Loop through all the proxies and look for the main game planets and moons
            foreach (var distanceProxy in distanceProxies)
            {
                // Get the corresponding cloned proxy body from the scene
                ProxyBody proxy = GameObject.Find(distanceProxy.proxyPrefab.name + "(Clone)")?.GetComponent<ProxyBody>();

                // If there is no corresponding proxy body in the scene, skip this proxy
                if (!proxy) continue;

                // Get the proxy name
                string proxyName = proxy.name;

                // Get the planet name by removing the proxy suffix
                string planetName = proxyName.Replace("_DistantProxy", "").Replace("(Clone)", "");

                // Clone the proxy model
                GameObject proxyModel = null;
                Clone(ref proxyModel, proxy);

                // Name the proxy model after the planet
                proxyModel.name = planetName;

                // Remove the ProxyOrbiter component from any moons
                foreach (ProxyOrbiter po in proxyModel.GetComponentsInChildren<ProxyOrbiter>())
                {
                    // Name the moon by removing the suffix
                    string moonName = po.transform.parent.name.Replace("_Pivot", "");
                    if (po.transform.name.Contains("_Body")) moonName = po.transform.name.Replace("_Body", "");

                    po.transform.name = moonName;

                    // Get the actual moon's model
                    GameObject moonModel = po._originalBody.gameObject;

                    // Reset proxy moon's parent
                    po.transform.parent.localPosition = Vector3.zero;
                    po.transform.parent.localRotation = Quaternion.identity;

                    // Add the moon model to the actual planets dictionary
                    if (moonModel) actualPlanets[moonName] = moonModel;

                    // Remove the proxy orbiter component
                    DestroyImmediate(po);
                }

                // Get the proxy's corresponding actual planet model
                GameObject planetModel = proxy._realObjectTransform.gameObject;

                // This is later changed in PlanetDisplay.cs but it allows the model to be scaled
                // to fit within the ship's cabin
                proxyModel.transform.localScale = Vector3.one * proxy._realObjectDiameter;

                // If there is no corresponding actual planet model, skip this proxy
                if (!planetModel) continue;

                // Add the poxy and actual planet to their respective dictionaries
                planetModels[planetName] = proxyModel;
                actualPlanets[planetName] = planetModel;

                ModHelper.Console.WriteLine($"Loaded proxy {planetName}", MessageType.Success);
            }

            // Group the twins together to act as the center of mass
            if (planetModels["EmberTwin"] != null && planetModels["AshTwin"] != null)
            {
                GameObject centerPivot = new GameObject("Twins Pivot");

                Transform sandStreamFromAshTwin = planetModels["AshTwin"].transform.Find("SandColumnRoot");
                float twinsDistance = 500.0f;

                planetModels["AshTwin"].transform.parent = centerPivot.transform;
                planetModels["AshTwin"].transform.localPosition = new Vector3(0.0f, twinsDistance * 0.5f, 0.0f);

                planetModels["EmberTwin"].transform.parent = centerPivot.transform;
                planetModels["EmberTwin"].transform.localPosition = new Vector3(0.0f, -twinsDistance * 0.5f, 0.0f);

                planetModels["Twins"] = centerPivot;
                actualPlanets["Twins"] = GameObject.Find("FocalBody");
            }

            // Create the sun as a child of the solar system node
            GameObject systemContainer = new GameObject("The Sun");

            GameObject sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sun.transform.parent = systemContainer.transform;
            sun.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
            sun.transform.localScale = new Vector3(3000.0f, 3000.0f, 3000.0f);
            sun.GetComponent<SphereCollider>().enabled = false;

            sun.transform.name = "Sun";

            // Create and set the material for the sphere
            Material sunMaterial = new Material(Shader.Find("Standard"));
            sunMaterial.EnableKeyword("_EMISSION");
            sunMaterial.SetColor("_EmissionColor", new Color(1f, 0.6f, 0f) * 1.5f);
            sun.GetComponent<MeshRenderer>().material = sunMaterial;

            // Store the system container
            planetModels["The Sun"] = systemContainer;

            ModHelper.Console.WriteLine($"Created {planetModels.Count} planet models", MessageType.Success);

            // Call the planet display manager to setup the actual display
            planetDisplay = PlanetDisplay.Create(planetModels, actualPlanets, shipCabinTransform, ModHelper.Console);

            // Apply the current config settings
            bool atmospheresEnabled = ModHelper.Config.GetSettingsValue<string>("planetAtmospheres") == "Enabled";
            bool cometTrailsEnabled = ModHelper.Config.GetSettingsValue<string>("interloperEffects") == "Enabled";

            planetDisplay.UpdateSettings(atmospheresEnabled, cometTrailsEnabled);
        }

        // Credit to MegaPiggy for the cloning and showProxy method, which allows us to clone the proxy bodies so they
        // can be used as holograms in the ship.
        // The Clone and showProxy functons are from AdvancedMinimap.cs file in the General Enhancements mod:
        // https://github.com/MegaPiggy/SBtT.GeneralEnhancements/
        void Clone(ref GameObject field, ProxyBody toClone)
        {
            toClone.gameObject.SetActive(false);
            field = Instantiate(toClone.gameObject);
            field.name = field.name.Replace("_DistantProxy", "").Replace("(Clone)", "");
            DestroyImmediate(field.GetComponent<ProxyBody>());
            field.gameObject.SetActive(true);
            showProxy(field);
            toClone.gameObject.SetActive(true);
        }

        void showProxy(GameObject proxyObj)
        {
            var proxies = proxyObj.GetComponentsInChildren<Transform>(true);

            foreach (Transform proxy in proxies)
            {
                if (proxy.name.Contains("Atmo") || proxy.name.Contains("Fog") || proxy.name.Contains("Effect")) continue;

                if (!proxy.TryGetComponent(out MeshRenderer rndr)) continue;
                rndr.enabled = true;

                if (proxy.name == "BlackHoleRenderer" || proxy.name == "WhiteHoleRenderer" || proxy.name == "Singularity") continue;

                /*if (rndr.sharedMaterials.Length == 1)
                {
                    rndr.sharedMaterial = GEAssets.MinimapMat;
                }
                else
                {
                    var mats = rndr.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = GEAssets.MinimapMat;
                    rndr.sharedMaterials = mats;
                }*/
            }

            proxyObj.SetActive(false);
        }
    }

}
