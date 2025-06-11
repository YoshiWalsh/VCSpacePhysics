using CG.Game.Player;
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
        [HarmonyPostfix, HarmonyPatch(typeof(CustomCharacterLocomotion), nameof(CustomCharacterLocomotion.Awake))]
        static void CustomCharacterLocomotionAwake(CustomCharacterLocomotion __instance)
        {
            var evaPhysics = __instance.gameObject.AddComponent<EVAPhysics>();
        }

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

        // Disable friction when the player is in space.
        // This also replicates default behaviour for how this value changes when the player
        // is seated, on board the ship, or walking on the ship's external surfaces. I wasn't
        // able to find where this is implemented in the base game, so I'm just overriding it here.
        [HarmonyPrefix, HarmonyPatch(typeof(CharacterLocomotion), nameof(CharacterLocomotion.UpdateExternalForces))]
        static void CharacterLocomotionUpdateExternalForces(CharacterLocomotion __instance)
        {
            var customLocomotion = (CustomCharacterLocomotion)__instance;
            var player = __instance.GetComponent<Player>();

            if (EVAUtils.IsPlayerSpaceborne(customLocomotion))
            {
                __instance.m_ExternalForceDamping = 0f;
            }
            else if (customLocomotion.DisableMovement)
            {
                __instance.m_ExternalForceDamping = 1000000000000000000000000000000000000f;
            }
            else if (player.IsInClearSpace)
            {
                __instance.m_ExternalForceDamping = 0.02f;
            }
            else {
                __instance.m_ExternalForceDamping = 0.1f;
            }
        }

        private struct CollisionDetectionState
        {
            public Vector3 positionDeltaBeforeCollisions;
        }

        // Store the player's intended position delta before processing collisions
        // so we can access it after collisions have been processed.
        [HarmonyPrefix, HarmonyPatch(typeof(CharacterLocomotion), nameof(CharacterLocomotion.DetectCollisions))]
        static void CharacterLocomotionDetectCollisionsPrefix(CharacterLocomotion __instance, ref CollisionDetectionState __state)
        {
            __state = new CollisionDetectionState()
            {
                positionDeltaBeforeCollisions = __instance.m_DesiredMovement
            };
        }

        // Update player's velocity and add damage
        [HarmonyPostfix, HarmonyPatch(typeof(CharacterLocomotion), nameof(CharacterLocomotion.DetectCollisions))]
        static void CharacterLocomotionDetectCollisionsPostfix(CharacterLocomotion __instance, ref CollisionDetectionState __state)
        {
            var positionDeltaBeforeCollisions = __state.positionDeltaBeforeCollisions;
            var positionDeltaAfterCollisions = __instance.m_DesiredMovement;

            var angleDeviation = Vector3.Angle(positionDeltaBeforeCollisions, positionDeltaAfterCollisions);
            if(angleDeviation < 0.05f)
            {
                // Player didn't significantly change direction, assume no collision detected
                return;
            }

            var positionDeltaRatio = positionDeltaAfterCollisions.magnitude / positionDeltaBeforeCollisions.magnitude;

            var newMomentum = positionDeltaAfterCollisions.normalized * __instance.m_ExternalForce.magnitude * positionDeltaRatio;
            __instance.m_ExternalForce = newMomentum;
        }
    }
}
