using Opsive.UltimateCharacterController.Character;
using System;
using System.Collections.Generic;
using System.Text;

namespace VCSpacePhysics.EVA
{
    public static class EVAUtils
    {
        public static bool IsPlayerJetpackEquipped(UltimateCharacterLocomotion character)
        {
            var playerGameObject = character.gameObject;
            var jetpack = playerGameObject.GetComponentInChildren<JetpackItem>();
            if (jetpack == null)
            {
                return false;
            }
            return jetpack.VisibleObjectActive;
        }

        public static bool IsPlayerSpaceborne(UltimateCharacterLocomotion character)
        {
            return !character.UseGravity && !character.Grounded;
        }

        public static bool IsPlayerFlying(UltimateCharacterLocomotion character)
        {
            return IsPlayerJetpackEquipped(character) && IsPlayerSpaceborne(character);
        }
    }
}
