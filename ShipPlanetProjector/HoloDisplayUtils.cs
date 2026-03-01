using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace ShipPlanetProjector
{
    public class HoloDisplayUtils : MonoBehaviour
    {
        public static void SetupPlanet(GameObject planet, Transform planetHolder, bool setScale = true)
        {
            planet.transform.SetParent(planetHolder, false);

            // Scale planet based on diameteer to fit in the ship's cabin
            if (setScale) planet.transform.localScale = Vector3.one * ((0.002f * 500.0f) / planet.transform.localScale.x);

            planet.transform.localPosition = Vector3.zero;
            planet.transform.localRotation = Quaternion.identity;

            if (planet.name == "GiantsDeep") planet.transform.localScale = Vector3.one * 0.0007f;
            if (planet.name == "QuantumMoon") planet.transform.localScale = Vector3.one * 0.004f;
            if (planet.name == "DarkBramble") planet.transform.localScale = Vector3.one * 0.001f;
            if (planet.name == "BrittleHollow") planet.transform.localScale = Vector3.one * 0.0015f;
            if (planet.name == "Comet") planet.transform.localScale = Vector3.one * 0.002f;
            if (planet.name == "The Sun") planet.transform.localScale = Vector3.one * 0.0002f;

            // Check every child and update them
            RecursiveSetupChildren(planet.transform, setScale ? planet.transform.localScale.x : 1.0f);
        }

        private static void RecursiveSetupChildren(Transform parent, float planetScaleFactor = 1.0f)
        {
            foreach (Transform child in parent)
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
                    lightning._lightRadius.min *= planetScaleFactor;
                    lightning._lightRadius.max *= planetScaleFactor;
                }

                // Check the current child for children and update them (repeats until there are no more children)
                RecursiveSetupChildren(child, planetScaleFactor);
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
    }
}
