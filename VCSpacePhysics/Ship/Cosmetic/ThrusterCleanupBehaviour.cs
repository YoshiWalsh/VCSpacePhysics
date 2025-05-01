using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VFX;

namespace VCSpacePhysics.Ship.Visual
{
    // This class only exists because ThrusterEffectPlayerInput doesn't have an OnDestroy method
    // for us to patch. Since we maintain references to ThrusterEffectPlayerInput instances within
    // a static variable, we need to make sure we remove the references whenever an instance is
    // destroyed, otherwise we would leak memory.
    class ThrusterCleanupBehaviour : MonoBehaviour
    {
        private ThrusterEffectPlayerInput thrusterEffect;

        public virtual void Awake()
        {
            thrusterEffect = this.gameObject.GetComponent<ThrusterEffectPlayerInput>();
        }

        public void OnDestroy()
        {
            ThrusterPatches.ThrusterMapping.Remove(thrusterEffect);
        }
    }
}
