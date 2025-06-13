using CG.Game.Player;
using HarmonyLib;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Game;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using VCSpacePhysics.Character;

namespace VCSpacePhysics.Character.Physics
{
    [HarmonyPatch]
    public class LocomotionPatches
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
            __instance.AlignToUpDirection = !CharacterUtils.IsPlayerSpaceborne(__instance);
        }

        // TODO: I don't think this does anything. Test if it can be removed.
        [HarmonyPostfix, HarmonyPatch(typeof(CustomCharacterLocomotion), nameof(CustomCharacterLocomotion.Update))]
        static void CustomCharacterLocomotionUpdate(CustomCharacterLocomotion __instance)
        {
            if (CharacterUtils.IsPlayerFlying(__instance))
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
            if (CharacterUtils.IsPlayerFlying(__instance.m_CharacterLocomotion))
            {
                return false;
            }
            return true;
        }

        // TODO: Not sure if this does anything. Test if it can be removed.
        [HarmonyPrefix, HarmonyPatch(typeof(AlignToGravityZone), nameof(AlignToGravityZone.UpdateRotation))]
        static bool AlignToGravityZoneUpdateRotation(AlignToGravityZone __instance)
        {
            if (CharacterUtils.IsPlayerFlying(__instance.m_CharacterLocomotion))
            {
                return false;
            }
            return true;
        }

        // TODO: Not sure if this does anything. Test if it can be removed.
        [HarmonyPrefix, HarmonyPatch(typeof(AlignToGround), nameof(AlignToGround.Update))]
        static bool AlignToGroundUpdate(AlignToGround __instance)
        {
            if (CharacterUtils.IsPlayerFlying(__instance.m_CharacterLocomotion))
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

            if (CharacterUtils.IsPlayerSpaceborne(customLocomotion))
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

        // Test to see if this is what makes the player get downwards velocity when leaving ship hull
        // // When player leaves ship hull, apply momentum
        [HarmonyPrefix, HarmonyPatch(typeof(CustomCharacterLocomotion), nameof(CustomCharacterLocomotion.ApplyEjectionVelocity))]
        static bool CustomCharacterLocomotionApplyEjectionVelocity()
        {
            // TODO: apply ship velocity
            return false;
        }

        // Ensure can-jump tests uses player-local directions
        [HarmonyTranspiler, HarmonyPatch(typeof(Jump), nameof(Jump.CanStartAbility))]
        static IEnumerable<CodeInstruction> JumpCanStartAbilityTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            return codeMatcher
                .MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(CharacterLocomotion), nameof(CharacterLocomotion.Up))))
                .Repeat(matcher =>
                    matcher
                        .RemoveInstruction()
                        .InsertAndAdvance(
                            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(CharacterLocomotion), nameof(CharacterLocomotion.m_Transform))),
                            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.up)))
                        )
                )
                .InstructionEnumeration();
        }

        private struct GroundCollisionDetectionState
        {
            public Vector3 gravityDirection;
        }

        static bool runningDetectGroundCollision = false;
        // Void Crew by default tests to see if the player is standing on a space platform by casting along worldspace down.
        // This code changes the logic to use player-relative down. This means the player is able to land on things that are below their feet.
        [HarmonyPrefix, HarmonyPatch(typeof(CharacterLocomotion), nameof(CharacterLocomotion.DetectGroundCollision))]
        static bool CharacterLocomotionDetectGroundCollision(CharacterLocomotion __instance, ref GroundCollisionDetectionState __state)
        {
            runningDetectGroundCollision = true;

            __state = new GroundCollisionDetectionState()
            {
                gravityDirection = __instance.m_GravityDirection
            };

            var customLocomotion = (CustomCharacterLocomotion)__instance;
            if (CharacterUtils.IsPlayerSpaceborne(customLocomotion))
            {
                __instance.m_GravityDirection = __instance.gameObject.transform.up * -1;
            }
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CharacterLocomotion), nameof(CharacterLocomotion.DetectGroundCollision))]
        static void CharacterLocomotionDetectGroundCollisionPostfix(CharacterLocomotion __instance, ref GroundCollisionDetectionState __state)
        {
            runningDetectGroundCollision = false;

            __instance.m_GravityDirection = __state.gravityDirection;
        }

        // Since we modified the player to have their grounding cast use local-down, this means the cast might hit the undersides of objects
        // if the player is upside-down compared to them. This can cause the player to warp to the top of the object, which is undesired.
        // To avoid this, we will prevent grounding the player unless their rotation is similar to that of the platform.
        // The easiest way to do this is to filter the raycast results to only include game objects with similar rotations.
        [HarmonyPostfix, HarmonyPatch(typeof(CharacterLocomotion), nameof(CharacterLocomotion.CombinedCast))]
        static void CharacterLocomotionCombinedCast(CharacterLocomotion __instance, ref int __result)
        {
            var customLocomotion = (CustomCharacterLocomotion)__instance;
            if (!CharacterUtils.IsPlayerSpaceborne(customLocomotion))
            {
                return;
            }
            if (runningDetectGroundCollision)
            {
                var count = 0;
                for(var i = 0; i < __result; i++)
                {
                    var r = __instance.m_CombinedCastResults[i];
                    if (r.transform is not null)
                    {
                        var angle = Vector3.Angle(__instance.gameObject.transform.up, r.transform.up);
                        if (angle > 45f) // Only allow the player to become grounded on platforms that roughly match their rotation
                        {
                            continue;
                        }
                    }
                    __instance.m_CombinedCastResults[count++] = r;
                    __result = count;
                }
            }
        }

        // TODO: Need to vertically align player when near a gravity zone
    }
}
