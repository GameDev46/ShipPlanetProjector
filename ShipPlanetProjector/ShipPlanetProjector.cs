using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;
using UnityEngine.Tilemaps;

using System;
using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;
using OWML.Utils;
using Steamworks;
using TMPro;

namespace ShipPlanetProjector
{
    public class ShipPlanetProjector : ModBehaviour
    {
        public static ShipPlanetProjector Instance;

        private static readonly Dictionary<string, GameObject> CachedGameObjects = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, GameObject> CachedRootGameObjects = new Dictionary<string, GameObject>();

        private bool hasInitialised = false;
        private bool isInSolarSystem = false;

        Dictionary<string, GameObject> planetModels = new Dictionary<string, GameObject>();

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
            isInSolarSystem = false;
            if (newScene != OWScene.SolarSystem) return;

            ModHelper.Events.Unity.FireOnNextUpdate(() =>
            {
                hasInitialised = false;
                isInSolarSystem = true;
            });
        }

        public void Update()
        {
            // Only initialise when the player has loaded into the actual game
            if (!hasInitialised && isInSolarSystem)
            {
                hasInitialised = true;
                SetupPlanetProjector();
            }
        }

        private void SetupPlanetProjector()
        {
            Transform shipTransform = Locator.GetShipTransform();
            Transform hologramParent = shipTransform.Find("Module_Cabin");

            if (shipTransform == null)
            {
                ModHelper.Console.WriteLine("Couldn't locate the ship's transform", MessageType.Error);
                return;
            }

            if (hologramParent == null)
            {
                ModHelper.Console.WriteLine("Couldn't locate the ship's cabin module", MessageType.Error);
                return;
            }

            // Find all the proxy body objects
            var proxies = FindObjectsOfType<ProxyBody>();

            // Store the twin gameobjects so they can be grouped
            GameObject emberTwin = null;
            GameObject ashTwin = null;
               
            // Clear the planet models dictionary
            planetModels = new Dictionary<string, GameObject>();

            // Loop through all the proxies and look for the main game planets and moons
            foreach (var proxy in proxies)
            {
                string n = proxy.name;
                //if (!n.Contains("(Clone)")) continue;
                if (n.Contains("BakedTerrain")) continue;
                if (n.Contains("ProxyFragment")) continue;
                if (n.Contains("Chunk")) continue;
                if (n.Contains("to")) continue;

                if (n.Contains("EmberTwin"))
                {
                    Clone(ref emberTwin, proxy);
                }
                else if (n.Contains("AshTwin"))
                {
                    Clone(ref ashTwin, proxy);
                }
                else if (n.Contains("TimberHearth"))
                {
                    GameObject timberHearthModel = null;
                    Clone(ref timberHearthModel, proxy);

                    GameObject THMoon = timberHearthModel.GetComponentInChildren<ProxyOrbiter>(true).gameObject;
                    DestroyImmediate(THMoon.GetComponent<ProxyOrbiter>());
                    THMoon.name = "TimberMoon";

                    planetModels["Timber Hearth"] = timberHearthModel;
                }
                else if (n.Contains("BrittleHollow"))
                {
                    GameObject brittleHollow = null;
                    Clone(ref brittleHollow, proxy);

                    GameObject BHMoon = brittleHollow.GetComponentInChildren<ProxyOrbiter>(true).gameObject;
                    DestroyImmediate(BHMoon.GetComponent<ProxyOrbiter>());
                    BHMoon.name = "VolcanicMoon";

                    planetModels["Brittle Hollow"] = brittleHollow;
                }
                else if (n.Contains("GiantsDeep"))
                {
                    GameObject giantsDeep = null;
                    Clone(ref giantsDeep, proxy);
                    planetModels["Giant's Deep"] = giantsDeep;
                }
                else if (n.Contains("DarkBramble"))
                {
                    GameObject darkBramble = null;
                    Clone(ref darkBramble, proxy);
                    planetModels["Dark Bramble"] = darkBramble;
                }
                else if (n.Contains("Comet"))
                {
                    GameObject comet = null;
                    Clone(ref comet, proxy);
                    planetModels["The Interloper"] = comet;
                }
                else if (n.Contains("WhiteHole"))
                {
                    GameObject whiteHole = null;
                    Clone(ref whiteHole, proxy);
                    planetModels["White Hole"] = whiteHole;
                }
                else if (n.Contains("QuantumMoon"))
                {
                    GameObject quantumMoon = null;
                    Clone(ref quantumMoon, proxy);
                    planetModels["Quantum Moon"] = quantumMoon;
                }

            }

            // Group the twins together to act as the center of mass
            if (emberTwin != null && ashTwin != null)
            {
                GameObject centerPivot = new GameObject("Twins Pivot");

                Transform sandStreamFromAshTwin = ashTwin.transform.Find("SandColumnRoot");
                float twinsDistance = 500.0f;

                ashTwin.transform.parent = centerPivot.transform;
                ashTwin.transform.localPosition = new Vector3(0.0f, twinsDistance * 0.5f, 0.0f);

                emberTwin.transform.parent = centerPivot.transform;
                emberTwin.transform.localPosition = new Vector3(0.0f, -twinsDistance * 0.5f, 0.0f);

                planetModels["Twins"] = centerPivot;
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
            PlanetDisplay.Create(planetModels, hologramParent, ModHelper.Console);
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
