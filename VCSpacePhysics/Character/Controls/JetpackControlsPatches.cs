using HarmonyLib;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.FirstPersonController.Camera.ViewTypes;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using CG.Game.Player;
using VCSpacePhysics.Character.Physics;
using CG.Input;
using VCSpacePhysics.Utils;

namespace VCSpacePhysics.Character.Controls
{
    [HarmonyPatch]
    public class JetpackControlsPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(CustomFirstPersonCombat), nameof(CustomFirstPersonCombat.Rotate))]
        static void CustomFirstPersonCombatRotate(CustomFirstPersonCombat __instance)
        {
            var evaPhysics = __instance.m_CharacterLocomotion.GetComponent<EVAPhysics>();
            evaPhysics._firstPersonView = __instance; // This is super ugly but it's easy, please don't judge me for a moment of weakness, a single transgression
            __instance.YawLimit = 90f;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(FlyJetpack), nameof(FlyJetpack.EnableInput))]
        static void EnableInput(FlyJetpack __instance)
        {
            if (__instance.localPlayer.IsMine && __instance.localPlayer.HasJetpack)
            {
                Plugin.logger.LogError("Enabling input");
                var evaPhysics = __instance.m_CharacterLocomotion.GetComponent<EVAPhysics>();
                evaPhysics.EnableInput();
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(FlyJetpack), nameof(FlyJetpack.DisableInput))]
        static void DisableInput(FlyJetpack __instance)
        {
            if (__instance.localPlayer.IsMine && __instance.localPlayer.HasJetpack)
            {
                Plugin.logger.LogError("Disabling input");
                var evaPhysics = __instance.m_CharacterLocomotion.GetComponent<EVAPhysics>();
                evaPhysics.DisableInput();
            }
        }
    }
}
