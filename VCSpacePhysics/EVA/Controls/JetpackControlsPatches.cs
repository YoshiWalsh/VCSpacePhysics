using HarmonyLib;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.FirstPersonController.Camera.ViewTypes;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using CG.Game.Player;

namespace VCSpacePhysics.EVA.Controls
{
    [HarmonyPatch]
    public class JetpackControlsPatches
    {
        private static Vector3 CenterOfGravityOffset = new Vector3(0, 1.2f, 0f);

        private static void RotatePlayerPositionAroundCenterOfGravity(CharacterLocomotion character, Quaternion rotation)
        {
            Vector3 characterCOGWorldspace = character.gameObject.transform.TransformPoint(CenterOfGravityOffset);
            Vector3 currentCharacterRootPosition = character.gameObject.transform.position;
            Vector3 currentRootPositionRelativeToCOG = currentCharacterRootPosition - characterCOGWorldspace;
            Vector3 newRootPositionRelativeToCOG = rotation * currentRootPositionRelativeToCOG;
            Vector3 newCharacterRootPosition = newRootPositionRelativeToCOG + characterCOGWorldspace;

            //var storedDesiredMovement = character.DesiredMovement;
            //var storedInstantMove = character.InstantRigidbodyMove;
            //var storedLocalDesiredMovement = character.LocalDesiredMovement;
            //var storedRootMotionDeltaPosition = character.RootMotionDeltaPosition;

            //character.DesiredMovement = newCharacterRootPosition - currentCharacterRootPosition;
            //character.InstantRigidbodyMove = true;
            //character.LocalDesiredMovement = Vector3.zero;
            //character.ApplyPosition();

            //character.DesiredMovement = storedDesiredMovement;
            //character.InstantRigidbodyMove = storedInstantMove;
            //character.LocalDesiredMovement =  storedLocalDesiredMovement;
            //character.RootMotionDeltaPosition =  storedRootMotionDeltaPosition;

            character.Rigidbody.position = character.gameObject.transform.position = newCharacterRootPosition;
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
    }
}
