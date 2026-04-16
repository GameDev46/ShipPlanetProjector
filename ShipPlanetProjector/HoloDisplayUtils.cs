using OWML.Common;
using OWML.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using static MonoMod.Cil.RuntimeILReferenceBag;

namespace ShipPlanetProjector
{
    public class HoloDisplayUtils : MonoBehaviour
    {
        // Used to determine whether the corresponding planet should be positioned relative to the sun / currently focused planet
        // or instead positioned relative to a parent body
        public bool hasParentPlanet = false;

        public float actualPlanetDiameter = 500.0f;
        public float diameterMultiplier = 1.0f;

        public float atmosphereInnerRadius = 0.0f;
        public float atmosphereOuterRadius = 0.0f;

        // Used for planets with child moons
        public List<GameObject> moons = new List<GameObject>();

        // Fragment management
        public GameObject realFragment;

        public static IModConsole modConsole;

        // Holds all the lightning generators in the scene for resizing
        private static List<CloudLightningGenerator> lightningGenerators = new List<CloudLightningGenerator>();

        private static List<MeteorLauncher> meteorLaunchers = new List<MeteorLauncher>();

        private static List<HoloDisplayUtils> atmosphereDisplayUtils = new List<HoloDisplayUtils>();
        private static List<MeshRenderer> atmosphereRenderers = new List<MeshRenderer>();

        public static void SetupUtils(IModConsole modCon)
        {
            lightningGenerators = new List<CloudLightningGenerator>();
            meteorLaunchers = new List<MeteorLauncher>();
            atmosphereDisplayUtils = new List<HoloDisplayUtils>();
            atmosphereRenderers = new List<MeshRenderer>();

            modConsole = modCon;
        }

        public static void SetupPlanet(GameObject planet, GameObject planetHolder, GameObject fragmentHolder, float projectorScale)
        {
            planet.transform.SetParent(planetHolder.transform, false);

            planet.transform.localPosition = Vector3.zero;
            planet.transform.localRotation = Quaternion.identity;
            planet.transform.localScale = Vector3.one;

            ProjectorFragmentManager fragmentManager = fragmentHolder.AddComponent<ProjectorFragmentManager>();
            fragmentManager.Setup(fragmentHolder, modConsole);

            // Check every child and update them
            RecursiveSetupChildren(planet, planet, planetHolder, fragmentManager, projectorScale);

            // Collect all the fragements
            HoloDisplayUtils[] allFragments = planet.GetComponentsInChildren<HoloDisplayUtils>(true);

            foreach (HoloDisplayUtils possibleFragment in allFragments)
            {
                GameObject realFragment = possibleFragment.realFragment;
                if (realFragment) fragmentManager.AddFragment(possibleFragment.transform.gameObject, realFragment);
            }

            // If no fragments were found then kill the fragment manager
            if (fragmentManager.actualFragments.Count <= 0) DestroyImmediate(fragmentManager);
        }

        private static void RecursiveSetupChildren(GameObject parent, GameObject root, GameObject planetHolder, ProjectorFragmentManager fragmentManager, float projectorScale)
        {
            foreach (Transform child in parent.transform)
            {
                // Enable the child
                child.gameObject.SetActive(true);

                // Disable the fog sphere child of planets as it causes visual issues when rendered in the ship's cabin
                if (child.name == "FogSphere") child.gameObject.SetActive(false);

                if (child.TryGetComponent<MeshRenderer>(out MeshRenderer renderer)) {
                    // Enable the child's renderer
                    renderer.enabled = true;

                    // Get the child's material
                    Material rendererMat = renderer.material;

                    if (rendererMat)
                    {
                        // Update cloud fade distance (x: fade start, y: fade end, z: unused, w: unused)
                        rendererMat.SetVector("_FadeDist", new Vector4(0.0f, 0.0f, 0.0f, 0.0f));

                        // Set the fade distances to 0 to prevent the tails being invisble when close up for comets
                        rendererMat.SetFloat("_CameraFadeDist", 0.0f);
                        rendererMat.SetFloat("_GeomFadeDist", 0.0f);

                        float innerRad = 0.0f;
                        float outerRad = 0.0f;

                        if (root.gameObject.TryGetComponent<HoloDisplayUtils>(out HoloDisplayUtils holoDisplayUtils))
                        {
                            if (rendererMat.HasProperty("_InnerRadius") && rendererMat.HasProperty("_OuterRadius"))
                            {
                                holoDisplayUtils.atmosphereInnerRadius = rendererMat.GetFloat("_InnerRadius");
                                holoDisplayUtils.atmosphereOuterRadius = rendererMat.GetFloat("_OuterRadius");

                                // Scale the atmosphere to fit with the model properly
                                innerRad = rendererMat.GetFloat("_InnerRadius");
                                outerRad = rendererMat.GetFloat("_OuterRadius");

                                atmosphereDisplayUtils.Add(holoDisplayUtils);
                                atmosphereRenderers.Add(renderer);
                            }
                        }

                        rendererMat.SetFloat("_InnerRadius", innerRad * projectorScale);
                        rendererMat.SetFloat("_OuterRadius", outerRad * projectorScale);

                    }
                }

                // Remove child's colliders
                if (child.TryGetComponent<SphereCollider>(out SphereCollider sphereColl)) DestroyImmediate(sphereColl);
                if (child.TryGetComponent<Collider>(out Collider coll)) DestroyImmediate(coll);

                // Enable any lightning generators and scale their radius to fit the smaller display scale
                if (child.TryGetComponent<CloudLightningGenerator>(out CloudLightningGenerator lightning))
                {
                    // Enable the lightning generator
                    lightning.enabled = true;

                    // Scale the lighting's radius to fit the smaller display scale
                    lightning._lightRadius.min = 300.0f * projectorScale;
                    lightning._lightRadius.max = 700.0f * projectorScale;

                    lightningGenerators.Add(lightning);
                }

                if (child.name == "Terrain_Proxy_TT_Arch") child.localRotation = Quaternion.identity;
                if (child.name == "Terrain_Proxy_TT_Base") child.localRotation = Quaternion.identity;

                // Check the current child for children and update them (repeats until there are no more children)
                RecursiveSetupChildren(child.gameObject, root, planetHolder, fragmentManager, projectorScale);
            }
        }

        public static void SetDisplayScale(GameObject planetDisplay, GameObject focusedPlanet)
        {
            HoloDisplayUtils focusedHDU = focusedPlanet.transform.GetComponent<HoloDisplayUtils>();
            planetDisplay.transform.localScale = Vector3.one * ((0.002f * 500.0f) / (focusedHDU.actualPlanetDiameter * focusedHDU.diameterMultiplier));

            // Custom scales for certain planets
            if (focusedPlanet.name == "GiantsDeep") planetDisplay.transform.localScale = Vector3.one * 0.0007f;
            if (focusedPlanet.name == "QuantumMoon") planetDisplay.transform.localScale = Vector3.one * 0.002f;
            if (focusedPlanet.name == "DarkBramble") planetDisplay.transform.localScale = Vector3.one * 0.001f;
            if (focusedPlanet.name == "BrittleHollow") planetDisplay.transform.localScale = Vector3.one * 0.0015f;
            if (focusedPlanet.name == "Comet") planetDisplay.transform.localScale = Vector3.one * 0.002f;
            if (focusedPlanet.name == "The Sun") planetDisplay.transform.localScale = Vector3.one * 0.0002f;

            foreach (CloudLightningGenerator lightning in lightningGenerators)
            {
                // Scale the lighting's radius to fit the smaller display scale
                lightning._lightRadius.min = Mathf.Min(300.0f * planetDisplay.transform.localScale.x, 0.45f);
                lightning._lightRadius.max = Mathf.Min(700.0f * planetDisplay.transform.localScale.x, 1.05f);
            }

            for (int i = 0; i < atmosphereRenderers.Count; i++)
            {
                MeshRenderer atmosphereRenderer = atmosphereRenderers[i];
                HoloDisplayUtils atmosphereHDU = atmosphereDisplayUtils[i];

                // Get the child's material
                Material rendererMat = atmosphereRenderer.material;

                if (rendererMat)
                {
                    // Update the scale of the atmosphere to fit with the model properly
                    rendererMat.SetFloat("_InnerRadius", atmosphereHDU.atmosphereInnerRadius * planetDisplay.transform.localScale.x);
                    rendererMat.SetFloat("_OuterRadius", atmosphereHDU.atmosphereOuterRadius * planetDisplay.transform.localScale.x);
                }
            }
        }

        public static void SetHologramPositionAndRotation(GameObject hologram, GameObject actualPlanet, GameObject referencePlanet)
        {
            hologram.transform.localPosition = referencePlanet.transform.InverseTransformPoint(actualPlanet.transform.position);
            hologram.transform.localRotation = Quaternion.Inverse(referencePlanet.transform.rotation) * actualPlanet.transform.rotation;
        }

        public static void SetHologramPosition(GameObject hologram, GameObject actualPlanet, GameObject referencePlanet)
        {
            hologram.transform.localPosition = referencePlanet.transform.InverseTransformPoint(actualPlanet.transform.position);
        }

        public static void SetHologramRotation(GameObject hologram, GameObject actualPlanet, GameObject referencePlanet)
        {
            hologram.transform.localRotation = Quaternion.Inverse(referencePlanet.transform.rotation) * actualPlanet.transform.rotation;
        }

        public static void UpdatePlanetSettings(GameObject planet, bool atmospheresEnabled, bool cometTrailsEnabled)
        {
            // Check every child
            RecursiveUpdateSettings(planet.transform, atmospheresEnabled, cometTrailsEnabled);
        }

        private static void RecursiveUpdateSettings(Transform parent, bool atmosphereEnabled, bool cometTrailsEnabled)
        {
            // Check the child's name for keywords
            foreach (Transform child in parent)
            {
                // Enable / disable the child's renderer based on its name and the corresponding setting
                if (child.TryGetComponent<MeshRenderer>(out MeshRenderer renderer))
                {
                    if (child.name.Contains("Atmo")) renderer.enabled = atmosphereEnabled;
                    if (child.name.Contains("Tail")) renderer.enabled = cometTrailsEnabled;
                }

                // Check the current child for children and update them (repeats until there are no more children)
                RecursiveUpdateSettings(child, atmosphereEnabled, cometTrailsEnabled);
            }
        }

        public static List<MeteorLauncher> FindMeteorLaunchers(GameObject planet)
        {
            meteorLaunchers = new List<MeteorLauncher>();

            // If the planet does not exist then return an empty list
            if (!planet?.transform) return meteorLaunchers;

            // Check every child for a meteor launcher
            RecursiveFindMeteorLaunchers(planet.transform);

            // Return the found meteor launchers
            return meteorLaunchers;
        }

        private static void RecursiveFindMeteorLaunchers(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.TryGetComponent<MeteorLauncher>(out MeteorLauncher launcher))
                {
                    meteorLaunchers.Add(launcher);
                }

                RecursiveFindMeteorLaunchers(child);
            }
        }
    }
}
