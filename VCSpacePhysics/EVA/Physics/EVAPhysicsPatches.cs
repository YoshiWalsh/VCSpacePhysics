using HarmonyLib;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VCSpacePhysics.EVA;

namespace VCSpacePhysics.EVA.Physics
{
    [HarmonyPatch]
    public class EVAPhysicsPatches
    {
        // This patch prevents the CharacterLocomotion and FirstPerson classes from snapping the
        // player to be globally-upright whenever they are not on a moving space platform.
        // It's important to re-enable the keep-upright behaviour when the player is grounded, because
        // otherwise they carry wonky rotations with them when they re-board the ship after an EVA.
        [HarmonyPostfix, HarmonyPatch(typeof(CustomCharacterLocomotion), nameof(CustomCharacterLocomotion.SetMovingPlatform))]
        static void CustomCharacterLocomotionSetMovingPlatform(CustomCharacterLocomotion __instance, Transform platform, bool platformOverride = true)
        {
            __instance.AlignToUpDirection = !EVAUtils.IsPlayerSpaceborne(__instance);
        }

        // TODO: I don't think this does anything. Test if it can be removed.
        [HarmonyPostfix, HarmonyPatch(typeof(CustomCharacterLocomotion), nameof(CustomCharacterLocomotion.Update))]
        static void CustomCharacterLocomotionUpdate(CustomCharacterLocomotion __instance)
        {
            if (EVAUtils.IsPlayerFlying(__instance))
            {
                __instance.transform.rotation = __instance.LookSource.Transform.rotation;
            }
        }

        // TODO: I don't think this does anything. Test if it can be removed.
        // TODO: Attempt at avoiding the player from being launched/randomly rotated
        // sometimes when they leave the ship's hull.
        [HarmonyPrefix, HarmonyPatch(typeof(CharacterLocomotion), nameof(CharacterLocomotion.UpdateMovingPlaformDisconnectMovement))]
        static bool CharacterLocomotionUpdateMovingPlaformDisconnectMovement()
        {
            return false;
        }

        // TODO: Not sure if this does anything. Test if it can be removed.
        [HarmonyPrefix, HarmonyPatch(typeof(AlignToPlatformGravityZone), nameof(AlignToPlatformGravityZone.UpdateRotation))]
        static bool AlignToPlatformGravityZoneUpdateRotation(AlignToPlatformGravityZone __instance)
        {
            if (EVAUtils.IsPlayerFlying(__instance.m_CharacterLocomotion))
            {
                return false;
            }
            return true;
        }

        // TODO: Not sure if this does anything. Test if it can be removed.
        [HarmonyPrefix, HarmonyPatch(typeof(AlignToGravityZone), nameof(AlignToGravityZone.UpdateRotation))]
        static bool AlignToGravityZoneUpdateRotation(AlignToGravityZone __instance)
        {
            if (EVAUtils.IsPlayerFlying(__instance.m_CharacterLocomotion))
            {
                return false;
            }
            return true;
        }

        // TODO: Not sure if this does anything. Test if it can be removed.
        [HarmonyPrefix, HarmonyPatch(typeof(AlignToGround), nameof(AlignToGround.Update))]
        static bool AlignToGroundUpdate(AlignToGround __instance)
        {
            if (EVAUtils.IsPlayerFlying(__instance.m_CharacterLocomotion))
            {
                return false;
            }
            return true;
        }
    }
}
