using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace VCSpacePhysics.Environment
{
    [HarmonyPatch]
    public class PlayableAreaPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(PlayableAreaController), nameof(PlayableAreaController.FixedUpdate))]
        static bool PlayableAreaControllerFixedUpdate()
        {
            return false;
        }
    }
}
