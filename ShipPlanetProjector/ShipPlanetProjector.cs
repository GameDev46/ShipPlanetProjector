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

            List<GameObject> proxyGameObjects = new List<GameObject>();
            List<bool> wasNHProxy = new List<bool>();

            // Format the proxies
            foreach (var distanceProxy in distanceProxies)
            {
                // Get the corresponding cloned proxy body from the scene
                GameObject proxyGO = GameObject.Find(distanceProxy.proxyPrefab.name + "(Clone)");
                ProxyBody proxy = proxyGO?.GetComponent<ProxyBody>();

                if (proxy)
                {
                    proxyGameObjects.Add(proxyGO);
                    wasNHProxy.Add(false);
                }
            }

            // Attempt to locate the NHProxy component at runtime
            Type nhProxyType = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == "NHProxy");

            if (nhProxyType != null)
            {
                // Grab all NHProxy instances
                var allNHProxies = GameObject.FindObjectsOfType(nhProxyType);

                foreach (var proxy in allNHProxies)
                {
                    proxyGameObjects.Add(((Component)proxy).transform.gameObject);
                    wasNHProxy.Add(true);
                }
            }

            // Clear the planet models and actual planets dictionaries
            planetModels = new Dictionary<string, GameObject>();
            actualPlanets = new Dictionary<string, GameObject>();

            int index = -1;

            // Loop through all the proxies and look for the main game planets and moons
            foreach (var proxyObj in proxyGameObjects)
            {
                // Increase the index
                index++;

                // Get the ProxyBody component
                ProxyBody proxy = proxyObj?.GetComponent<ProxyBody>();

                // If there is no corresponding proxy body in the scene, then check for NHProxy
                // This is an edge case, during testing it was never used
                if (!proxy)
                {
                    // If NHProxy doesn't exist then skip
                    if (nhProxyType == null) continue;

                    // Get the NHProxy component
                    Component nhProxy = proxyObj.GetComponent(nhProxyType);
                    if (nhProxy == null) continue;

                    // Attempt to get the "planet" property of NHProxy
                    PropertyInfo planetProp = nhProxyType.GetProperty(
                        "planet",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    );

                    if (planetProp == null) continue;

                    GameObject realPlanet = planetProp.GetValue(nhProxy) as GameObject;

                    if (realPlanet == null) continue;

                    // Attempt to get the "baseRealObjectDiameter" property of NHProxy
                    PropertyInfo objDiameterProp = nhProxyType.GetProperty(
                        "baseRealObjectDiameter",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    );

                    if (objDiameterProp == null) continue;

                    object value = objDiameterProp.GetValue(nhProxy);
                    if (value == null) continue;

                    float realPlanetDiameter = (float)value;

                    // Remove the NHProxy component
                    Destroy(nhProxy);

                    // Recreate the NHProxy component as a ProxyBody component
                    proxy = proxyObj.AddComponent<ProxyBody>();
                    proxy.name = proxyObj.name.Replace("_Proxy", "");
                    proxy._realObjectTransform = realPlanet.transform;
                    proxy._realObjectDiameter = realPlanetDiameter;
                }

                // Get the proxy name
                string proxyName = proxy.name;

                // Get the planet name by removing the proxy suffix
                string planetName = proxyName.Replace("_DistantProxy", "").Replace("(Clone)", "").Replace("_Proxy", "");

                // Clone the proxy model
                GameObject proxyModel = null;
                Clone(ref proxyModel, proxy);

                // Name the proxy model after the planet
                proxyModel.name = planetName;

                // Track whether the proxy is parented to another proxy
                HoloDisplayUtils proxyHDU = proxyModel.AddComponent<HoloDisplayUtils>();
                proxyHDU.hasParentPlanet = false;
                proxyHDU.actualPlanetDiameter = proxy._realObjectDiameter;
                proxyHDU.diameterMultiplier = 1.0f;
                proxyHDU.moons = new List<GameObject>();

                if (wasNHProxy[index]) proxyHDU.diameterMultiplier = 2.0f;

                // Remove the ProxyOrbiter component from any moons
                foreach (ProxyOrbiter po in proxyModel.GetComponentsInChildren<ProxyOrbiter>())
                {
                    // Name the moon by removing the suffix
                    string moonName = po.transform.parent.name.Replace("_Pivot", "");
                    if (po.transform.name.Contains("_Body")) moonName = po.transform.name.Replace("_Body", "");

                    po.transform.name = moonName;

                    // Get the actual moon's model
                    GameObject actualModel = po._originalBody.gameObject;

                    // Reset proxy moon's parent
                    po.transform.parent.localPosition = Vector3.zero;
                    po.transform.parent.localRotation = Quaternion.identity;

                    // Record that the moon is parented to another proxy (used for positioning)
                    HoloDisplayUtils moonHDU = po.transform.gameObject.AddComponent<HoloDisplayUtils>();
                    moonHDU.hasParentPlanet = true;

                    // Add the moon model to the actual planets dictionary
                    if (actualModel) actualPlanets[moonName] = actualModel;

                    // Add the moon to moons
                    proxyHDU.moons.Add(po.transform.gameObject);

                    // Remove the proxy orbiter component
                    DestroyImmediate(po);
                }

                // Get the proxy's corresponding actual planet model
                GameObject planetModel = proxy._realObjectTransform.gameObject;

                // If there is no corresponding actual planet model, skip this proxy
                if (!planetModel) continue;

                // Add the poxy and actual planet to their respective dictionaries
                planetModels[planetName] = proxyModel;
                actualPlanets[planetName] = planetModel;

                ModHelper.Console.WriteLine($"Loaded proxy {planetName}", MessageType.Success);
            }

            // Group the twins together to act as the center of mass
            if (planetModels.ContainsKey("EmberTwin") && planetModels.ContainsKey("AshTwin"))
            {
                GameObject centerPivot = new GameObject("Twins Pivot");

                HoloDisplayUtils pivotHDU = centerPivot.AddComponent<HoloDisplayUtils>();
                pivotHDU.hasParentPlanet = false;
                pivotHDU.actualPlanetDiameter = 600.0f;

                GameObject actualPivot = GameObject.Find("FocalBody");

                if (actualPivot)
                {
                    planetModels["Twins"] = centerPivot;
                    actualPlanets["Twins"] = GameObject.Find("FocalBody");
                }
            }

            if (planetModels.ContainsKey("TimberHearth"))
            {
                // Create the sun as a child of the solar system node
                GameObject sunHolder = new GameObject();
                sunHolder.transform.name = "The Sun";

                HoloDisplayUtils sunHDU = sunHolder.AddComponent<HoloDisplayUtils>();
                sunHDU.hasParentPlanet = false;
                sunHDU.actualPlanetDiameter = 3000.0f;

                GameObject sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sun.transform.name = "Sun";
                sun.transform.SetParent(sunHolder.transform, false);
                sun.transform.localPosition = Vector3.zero;
                sun.transform.localRotation = Quaternion.identity;
                sun.transform.localScale = Vector3.one * 3000.0f;
                sun.GetComponent<SphereCollider>().enabled = false;

                // Create and set the material for the sphere
                Material sunMaterial = new Material(Shader.Find("Standard"));
                sunMaterial.EnableKeyword("_EMISSION");
                sunMaterial.SetColor("_EmissionColor", new Color(1f, 0.6f, 0f) * 1.5f);
                sun.GetComponent<MeshRenderer>().material = sunMaterial;

                // Store the system container
                planetModels["The Sun"] = sunHolder;
            }

            ModHelper.Console.WriteLine($"Created {planetModels.Count} planet models", MessageType.Success);

            // Call the planet display manager to setup the actual display
            planetDisplay = PlanetDisplay.Create(planetModels, actualPlanets, shipCabinTransform, ModHelper.Console);

            StartCoroutine(WaitToUpdateSettings());
        }

        IEnumerator WaitToUpdateSettings()
        {
            yield return new WaitForSeconds(0.5f);

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
