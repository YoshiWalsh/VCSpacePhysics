using HarmonyLib;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.FirstPersonController.Camera.ViewTypes;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VCSpacePhysics.EVA.Controls
{
    [HarmonyPatch]
    public class JetpackControlsPatches
    {
        private static Vector3 CenterOfGravityOffset = new Vector3(0, 1.2f, 0f);

        private static void RotatePlayerPositionAroundCenterOfGravity(CharacterLocomotion character, Quaternion rotation)
        {
            Vector3 characterCOGWorldspace = character.gameObject.transform.TransformPoint(CenterOfGravityOffset);
            Vector3 currentRootPositionRelativeToCOG = character.gameObject.transform.position - characterCOGWorldspace;
            Vector3 newRootPositionRelativeToCOG = rotation * currentRootPositionRelativeToCOG;
            character.gameObject.transform.position = newRootPositionRelativeToCOG + characterCOGWorldspace;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(FirstPerson), nameof(FirstPerson.Rotate))]
        static void FirstPersonRotate(FirstPerson __instance, float horizontalMovement, float verticalMovement, bool immediateUpdate)
        {
            if (EVAUtils.IsPlayerFlying(__instance.m_CharacterLocomotion))
            {
                // Make looking up/down rotate the entire character, not just the head
                var rotation = Quaternion.AngleAxis(__instance.m_Pitch, __instance.m_CharacterLocomotion.transform.right);
                RotatePlayerPositionAroundCenterOfGravity(__instance.m_CharacterLocomotion, rotation);
                __instance.m_BaseRotation = rotation * __instance.m_BaseRotation;
                __instance.m_Pitch = 0f;

                // TODO: Not sure if this does anything...? Find out, write a comment if it does.
                // I wrote this when I was trying things out and idk if it makes a difference.
                var baseRotation = __instance.m_BaseRotation;
                var lookRotation = Quaternion.Euler(__instance.m_Pitch, __instance.m_Yaw, 0f);
                var totalRotation = baseRotation * lookRotation;
                __instance.m_CharacterLocomotion.transform.rotation = totalRotation;

            }
        }

        // TODO: I don't think this does anything. Test if it can be removed.
        [HarmonyPrefix, HarmonyPatch(typeof(CustomFirstPersonCombat), nameof(CustomFirstPersonCombat.Rotate))]
        static bool CustomFirstPersonCombatRotate(CustomFirstPersonCombat __instance)
        {
            __instance.m_UseLocalRotation = false;
            __instance.lockedRelativeLocalRotation = false;
            return true;
        }
    }
}
