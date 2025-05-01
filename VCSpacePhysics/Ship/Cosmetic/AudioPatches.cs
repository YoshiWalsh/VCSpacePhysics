using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace VCSpacePhysics.Ship.Cosmetic
{
    [HarmonyPatch]
    public class AudioPatches
    {
        // The ship has this "running noise" whenever it's in motion. This makes no sense.
        // I could tie it to acceleration instead of velocity, but the ship already has engine
        // and thruster noises so I don't see the point. This patch disables the sound completely.
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerShipMovementAudio), nameof(PlayerShipMovementAudio.Update))]
        static bool PlayerShipMovementAudioUpdate(PlayerShipMovementAudio __instance)
        {
            return false;
        }
    }
}
