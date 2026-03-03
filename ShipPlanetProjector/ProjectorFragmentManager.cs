using OWML.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem.HID;

namespace ShipPlanetProjector
{
    public class ProjectorFragmentManager : MonoBehaviour
    {
        public List<GameObject> actualFragments = new List<GameObject>();
        public List<GameObject> modelFragments = new List<GameObject>();

        private GameObject fragmentHolder;

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

        public void Setup(GameObject fragHolder, IModConsole con)
        {
            // Set the fragment holder
            fragmentHolder = fragHolder;

            // Update the mod console
            modConsole = con;

            // Clear the actual and model fragments list
            actualFragments = new List<GameObject>();
            modelFragments = new List<GameObject>();
        }

        public void AddFragment(GameObject modelFragment, GameObject realFragment)
        {
            // Add the fragments to their corresponding lists
            actualFragments.Add(realFragment);
            modelFragments.Add(modelFragment);

            modelFragment.SetActive(true);
            modelFragment.transform.SetParent(fragmentHolder.transform, false);
        }

        public void Update()
        {
            for (int i = 0; i < actualFragments.Count; i++)
            {
                // Position the fragment relative to the current focused planet
                modelFragments[i].transform.localPosition = focusedPlanetActual.transform.InverseTransformPoint(actualFragments[i].transform.position);
                modelFragments[i].transform.localRotation = Quaternion.Inverse(focusedPlanetActual.transform.rotation) * actualFragments[i].transform.rotation;
                modelFragments[i].transform.localScale = Vector3.one;

                // Enable the fragment model's mesh renderer
                modelFragments[i].GetComponent<MeshRenderer>()?.enabled = true;

                // Hide fragments which are outside the ship's cabin
                float mag = modelFragments[i].transform.localPosition.magnitude * displayScale;
                modelFragments[i].SetActive(mag <= DISPLAY_CUTOFF_DIST && displayPowered ? true : false);
            }
        }
    }
}
