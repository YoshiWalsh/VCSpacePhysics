using CG.Game.Player;
using Opsive.UltimateCharacterController.Character;
using System;
using System.Collections.Generic;
using System.Text;

namespace VCSpacePhysics.Character
{
    public static class CharacterUtils
    {
        public static bool IsPlayerJetpackEquipped(UltimateCharacterLocomotion character)
        {
            var player = character.GetComponent<Player>();
            return player.HasJetpack;
        }

        public static bool IsPlayerSpaceborne(UltimateCharacterLocomotion character)
        {
            var customLocomotion = (CustomCharacterLocomotion) character;
            return !character.UseGravity && !character.Grounded && !customLocomotion.DisableMovement;
        }

        public static bool IsPlayerFlying(UltimateCharacterLocomotion character)
        {
            return IsPlayerJetpackEquipped(character) && IsPlayerSpaceborne(character);
        }
    }
}
