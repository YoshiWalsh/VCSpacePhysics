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

namespace VCSpacePhysics.Character.Controls
{
    [HarmonyPatch]
    public class JetpackControlsPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(FirstPerson), nameof(FirstPerson.Rotate))]
        static void FirstPersonRotate(FirstPerson __instance, float horizontalMovement, float verticalMovement, bool immediateUpdate)
        {
            var evaPhysics = __instance.m_CharacterLocomotion.GetComponent<EVAPhysics>();
            evaPhysics._firstPersonView = __instance; // This is super ugly but it's easy, please don't judge me for a moment of weakness, a single transgression
        }
    }
}
