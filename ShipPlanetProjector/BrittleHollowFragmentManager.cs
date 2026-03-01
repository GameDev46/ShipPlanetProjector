using OWML.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ShipPlanetProjector
{
    public class BrittleHollowFragmentManager : MonoBehaviour
    {
        public MeshRenderer[] renderers;

        private GameObject hologramFragment;

        private bool hasDetached;
        private bool hasWarped;
        private bool hasFinished;

        public GameObject brittleHollowBody;
        public GameObject whiteHoleBody;

        public GameObject actualFragment;

        public GameObject hologramBrittleHollow;
        public GameObject hologramWhiteHole;

        IModConsole modConsole;
        public void Setup(ProxyBrittleHollowFragment proxyBrittleHollowFragment, GameObject BH, GameObject WH, IModConsole con)
        {
            renderers = proxyBrittleHollowFragment._renderers;
            hologramBrittleHollow = BH;
            hologramWhiteHole = WH;

            hologramFragment = proxyBrittleHollowFragment.transform.gameObject;

            modConsole = con;
        }

        public void SetRealFragment(DetachableFragment body)
        {
            actualFragment = body.gameObject;

            DetachableFragment realFrag = body;
            realFrag.OnDetachFragment += OnFragmentDetached;
            realFrag.OnChangeSector += OnFragmentWarped;
            realFrag.OnComeToRest += OnFragmentCameToRest;
        }

        private void OnFragmentDetached(OWRigidbody fragmentBody, OWRigidbody attachedBody)
        {
            hasDetached = true;
            if (hologramFragment) hologramFragment.transform.parent = hologramBrittleHollow.transform;
        }

        private void OnFragmentWarped(Sector newParentSector)
        {
            hasWarped = true;
            if (!hologramFragment) return;

            //foreach (var r in renderers) r.enabled = true;
            hologramFragment.transform.parent = hologramWhiteHole.transform;
            hologramFragment.transform.localPosition = Vector3.zero;
            hologramFragment.transform.rotation = Quaternion.Inverse(whiteHoleBody.transform.rotation) * actualFragment.transform.rotation;
            hologramFragment.transform.localScale = Vector3.one * 0.1f;
        }

        private void OnFragmentCameToRest(OWRigidbody anchorBody)
        {
            hasFinished = true;
            if (!hologramFragment) return;

            hologramFragment.transform.localPosition = whiteHoleBody.transform.InverseTransformPoint(actualFragment.transform.position);
            hologramFragment.transform.localRotation = Quaternion.Inverse(whiteHoleBody.transform.rotation) * actualFragment.transform.rotation;
            hologramFragment.transform.localScale = Vector3.one;
        }

        public void Update()
        {
            if (!hologramFragment) return;

            if (hasDetached && !hasWarped)
            {
                hologramFragment.transform.localPosition = brittleHollowBody.transform.InverseTransformPoint(actualFragment.transform.position);
                hologramFragment.transform.localRotation = Quaternion.Inverse(brittleHollowBody.transform.rotation) * actualFragment.transform.rotation;
                hologramFragment.transform.localScale = Vector3.one;
            }

            if (hasWarped && !hasFinished)
            {
                hologramFragment.transform.localPosition = whiteHoleBody.transform.InverseTransformPoint(actualFragment.transform.position);
                hologramFragment.transform.rotation = Quaternion.Inverse(whiteHoleBody.transform.rotation) * actualFragment.transform.rotation;
                hologramFragment.transform.localScale = Vector3.one;
            }

        }
    }
}
