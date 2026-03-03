using OWML.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ShipPlanetProjector
{
    public class ProjectorMeteorManager : MonoBehaviour
    {
        private GameObject meteorHolder;
        private MeteorLauncher launcher;

        private List<GameObject> hologramMeteors = new List<GameObject>();
        private List<GameObject> actualMeteors = new List<GameObject>();
        private List<bool>  hologramMeteorsImpacted = new List<bool>();

        private IModConsole modConsole;

        private static GameObject focusedPlanetModel;
        private static GameObject focusedPlanetActual;

        private static float displayScale = 0.002f;

        private static float DISPLAY_CUTOFF_DIST = 3.0f;
        private static bool displayPowered = false;

        public static void SetFocusedPlanet(GameObject focusedPlanet, GameObject actualPlanet, float projectorScale)
        {
            focusedPlanetModel = focusedPlanet;
            focusedPlanetActual = actualPlanet;

            displayScale = projectorScale;
        }

        public static void SetProjectorCutOffDistance(float dist)
        {
            DISPLAY_CUTOFF_DIST = dist;
        }

        public static void SetDisplayPower(bool displayPower)
        {
            displayPowered = displayPower;
        }

        public void Setup(MeteorLauncher meteorLauncher, GameObject meteorParent, IModConsole con)
        {
            meteorHolder = meteorParent;
            launcher = meteorLauncher;

            modConsole = con;
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

                if (fakeMeteor.transform.Find("ConstantDetectors")) DestroyImmediate(fakeMeteor.transform.Find("ConstantDetectors").gameObject);
                if (fakeMeteor.transform.Find("DynamicDetector")) DestroyImmediate(fakeMeteor.transform.Find("DynamicDetector").gameObject);
                if (fakeMeteor.transform.Find("MeteorImpactLight")) DestroyImmediate(fakeMeteor.transform.Find("MeteorImpactLight").gameObject);
                if (fakeMeteor.transform.Find("MeteorShockLayer")) DestroyImmediate(fakeMeteor.transform.Find("MeteorShockLayer").gameObject);
                if (fakeMeteor.transform.Find("ImpactAudioSource")) DestroyImmediate(fakeMeteor.transform.Find("ImpactAudioSource").gameObject);

                if (fakeMeteor.transform.GetComponent<CenterOfTheUniverseOffsetApplier>()) DestroyImmediate(fakeMeteor.transform.GetComponent<CenterOfTheUniverseOffsetApplier>());
                if (fakeMeteor.transform.GetComponent<MeteorController>()) DestroyImmediate(fakeMeteor.transform.GetComponent<MeteorController>());
                if (fakeMeteor.transform.GetComponent<OWRigidbody>()) DestroyImmediate(fakeMeteor.transform.GetComponent<OWRigidbody>());
                if (fakeMeteor.transform.GetComponent<Rigidbody>()) DestroyImmediate(fakeMeteor.transform.GetComponent<Rigidbody>());

                ShowGameObject(fakeMeteor);

                fakeMeteor.transform.Find("MeteorGlowLight")?.GetComponent<Light>().range = 150.0f * 0.0015f;
                fakeMeteor.transform.Find("MeteorGlowLight")?.GetComponent<Light>().intensity = 1.0f;

                fakeMeteor.transform.Find("Meteor_Whole")?.gameObject.SetActive(true);

                fakeMeteor.transform.SetParent(meteorHolder.transform, false);

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
                hologramMeteors[index].transform.localPosition = focusedPlanetActual.transform.InverseTransformPoint(actualMeteors[index].transform.position);
                hologramMeteors[index].transform.localRotation = Quaternion.Inverse(focusedPlanetActual.transform.rotation) * actualMeteors[index].transform.rotation;
                hologramMeteors[index].transform.localScale = Vector3.one;

                // Hide meteors which are outside the ship's cabin
                float mag = hologramMeteors[index].transform.localPosition.magnitude * displayScale;
                hologramMeteors[index].SetActive(mag <= DISPLAY_CUTOFF_DIST && displayPowered ? true : false);
            }
        }

        public void Update()
        {
            // Check for any new meteors which need adding to the holoMap
            CheckToAddMeteors(launcher._launchedMeteors);
            // Update the meteors in the holoMap
            UpdateMeteors();
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
