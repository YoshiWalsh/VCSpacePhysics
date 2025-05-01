using Gameplay.Ship;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VFX;

namespace VCSpacePhysics.Ship.Visual
{
    [HarmonyPatch]
    public class ThrusterPatches
    {
        // Void Crew usually enables the thruster visual effects based on mapping each thruster to certain controls.
        // This is a problem for us, because we are remapping existing controls, as well as adding new
        // ways that the ship can rotate which the designers never accounted for.
        
        // The patches below rework the thruster visual effects to be based not on direct controls, but instead
        // based on the ship's applied thrust.


        [Flags]
        public enum ShipThrust // Represent all possible 6DOF movements, in both directions
        {
            None = 0,
            Forward = 2 << 0,
            Backward = 2 << 1,
            Left = 2 << 2,
            Right = 2 << 3,
            Up = 2 << 4,
            Down = 2 << 5,
            RollCounterclockwise = 2 << 6,
            RollClockwise = 2 << 7,
            PitchUp = 2 << 8,
            PitchDown = 2 << 9,
            YawLeft = 2 << 10,
            YawRight = 2 << 11,
        }

        // For each thruster effect object, store every type of thrust that should cause it to activate
        public static Dictionary<ThrusterEffectPlayerInput, ShipThrust> ThrusterMapping = new Dictionary<ThrusterEffectPlayerInput, ShipThrust>();


        // This patch runs once for each thruster effect object and works out which thrust directions should activate it.
        // This is calculated based on a combination of the designer-specified input mappings in the base game, and the
        // thruster's physical position on the ship.
        [HarmonyPostfix, HarmonyPatch(typeof(ThrusterEffectPlayerInput), nameof(ThrusterEffectPlayerInput.Awake), [])]
        static void ThrusterEffectPlayerInputAwake(ThrusterEffectPlayerInput __instance)
        {
            // How far does the thruster have to be from the ship's center-of-gravity
            // in order to be considered for torque movements.
            const float THRUSTER_POSITION_EPSILON = 0.5f;

            GameObject gameObject = __instance.gameObject;
            ShipThrust thrusterMovements = ShipThrust.None;

            var Ship = __instance.gameObject.GetComponentInParent<PlayerShip>();
            if (Ship != null)
            {
                // Irritatingly we can't just use the thruster's local position in order to find its offset
                // from the center of the ship, because the thrusters are nested under a game object that
                // for some reason is not aligned with the ship's game object.
                // On the Destroyer in particular it is misaligned by 2 units along the X axis, and this
                // caused issues for some of the roll thrusters.
                var thrusterPositionInShipSpace = Ship.gameObject.transform.InverseTransformPoint(gameObject.transform.position);


                // Thrusters that move the ship along the gameplay plane are only used for movement directions
                // supported by the base game, so we can map them simply.
                if (__instance.flags.HasFlag(ThrusterEffectPlayerInput.ThrustFlags.Input_Forward))
                {
                    thrusterMovements |= ShipThrust.Forward;
                }
                if (__instance.flags.HasFlag(ThrusterEffectPlayerInput.ThrustFlags.Input_Back))
                {
                    thrusterMovements |= ShipThrust.Backward;
                }
                if (__instance.flags.HasFlag(ThrusterEffectPlayerInput.ThrustFlags.Input_StrafeLeft))
                {
                    thrusterMovements |= ShipThrust.Left;
                }
                if (__instance.flags.HasFlag(ThrusterEffectPlayerInput.ThrustFlags.Input_StrafeRight))
                {
                    thrusterMovements |= ShipThrust.Right;
                }
                if (__instance.flags.HasFlag(ThrusterEffectPlayerInput.ThrustFlags.Input_TurnLeft))
                {
                    thrusterMovements |= ShipThrust.YawLeft;
                }
                if (__instance.flags.HasFlag(ThrusterEffectPlayerInput.ThrustFlags.Input_TurnRight))
                {
                    thrusterMovements |= ShipThrust.YawRight;
                }


                // Up/down-facing thrusters may be used for our new pitch/roll rotations,
                // so we need to perform some extra calculations based on thruster position to see
                // if any of these apply.
                if (__instance.flags.HasFlag(ThrusterEffectPlayerInput.ThrustFlags.Input_Up))
                {
                    thrusterMovements |= ShipThrust.Up;

                    if (thrusterPositionInShipSpace.x < 0 - THRUSTER_POSITION_EPSILON) // Left upwards thrusters
                    {
                        thrusterMovements |= ShipThrust.RollClockwise;
                    }
                    if (thrusterPositionInShipSpace.x > 0 + THRUSTER_POSITION_EPSILON) // Right upwards thrusters
                    {
                        thrusterMovements |= ShipThrust.RollCounterclockwise;
                    }
                    if (thrusterPositionInShipSpace.z < 0 - THRUSTER_POSITION_EPSILON) // Rear upwards thrusters
                    {
                        thrusterMovements |= ShipThrust.PitchDown;
                    }
                    if (thrusterPositionInShipSpace.z > 0 + THRUSTER_POSITION_EPSILON) // Front upwards thrusters
                    {
                        thrusterMovements |= ShipThrust.PitchUp;
                    }
                }
                if (__instance.flags.HasFlag(ThrusterEffectPlayerInput.ThrustFlags.Input_Down))
                {
                    thrusterMovements |= ShipThrust.Down;

                    if (thrusterPositionInShipSpace.x < 0 - THRUSTER_POSITION_EPSILON) // Left downwards thrusters
                    {
                        thrusterMovements |= ShipThrust.RollCounterclockwise;
                    }
                    if (thrusterPositionInShipSpace.x > 0 + THRUSTER_POSITION_EPSILON) // Right downwards thrusters
                    {
                        thrusterMovements |= ShipThrust.RollClockwise;
                    }
                    if (thrusterPositionInShipSpace.z < 0 - THRUSTER_POSITION_EPSILON) // Rear downwards thrusters
                    {
                        thrusterMovements |= ShipThrust.PitchUp;
                    }
                    if (thrusterPositionInShipSpace.z > 0 + THRUSTER_POSITION_EPSILON) // Front downwards thrusters
                    {
                        thrusterMovements |= ShipThrust.PitchDown;
                    }
                }
            }

            ThrusterMapping.Add(__instance, thrusterMovements);

            gameObject.AddComponent<ThrusterCleanupBehaviour>(); // Used to avoid memory leaks (see comment in ThrusterCleanupBehaviour.cs for details)


            // While we're here, let's make the thruster effects turn on and off quicker.
            // The default speed didn't look right with how agile the ship is. The ship
            // would start rotating before the thrusters visually turned on.
            __instance.PowerOnLerp = 0.2f;
            __instance.PowerOffLerp = 0.2f;
        }


        // This patch makes the thruster figure out its current thrust value based on our thrust-direction
        // lookup table, instead of based on the designer-specified input mapping.
        [HarmonyPrefix, HarmonyPatch(typeof(ThrusterEffectPlayerInput), nameof(ThrusterEffectPlayerInput.GetMaxThrustValue))]
        static bool ThrusterEffectPlayerInputGetMaxThrustValue(ThrusterEffectPlayerInput __instance, ref float __result, ThrusterEffectPlayerInput.ThrustFlags thrustFlags)
        {
            var thrusterMovements = ThrusterMapping.GetValueSafe(__instance);

            float maximumThrust = 0f;
            foreach (ShipThrust thrusterMovement in Enum.GetValues(typeof(ShipThrust)))
            {
                if (!thrusterMovements.HasFlag(thrusterMovement))
                {
                    continue;
                }

                float movementThrust = 0f;
                foreach (ShipEngine engine in __instance.engines)
                {
                    movementThrust += thrusterMovement switch
                    {
                        ShipThrust.Forward => Mathf.Clamp(engine.AppliedThrust.z, 0f, 1f),
                        ShipThrust.Backward => Mathf.Clamp(0f - engine.AppliedThrust.z, 0f, 1f),
                        ShipThrust.Right => Mathf.Clamp(engine.AppliedThrust.x, 0f, 1f),
                        ShipThrust.Left => Mathf.Clamp(0f - engine.AppliedThrust.x, 0f, 1f),
                        ShipThrust.Up => Mathf.Clamp(engine.AppliedThrust.y, 0f, 1f),
                        ShipThrust.Down => Mathf.Clamp(0f - engine.AppliedThrust.y, 0f, 1f),
                        ShipThrust.RollClockwise => Mathf.Clamp(0f - engine.AppliedTorque.z, 0f, 1f),
                        ShipThrust.RollCounterclockwise => Mathf.Clamp(engine.AppliedTorque.z, 0f, 1f),
                        ShipThrust.PitchUp => Mathf.Clamp(0f - engine.AppliedTorque.x, 0f, 1f),
                        ShipThrust.PitchDown => Mathf.Clamp(engine.AppliedTorque.x, 0f, 1f),
                        ShipThrust.YawRight => Mathf.Clamp(engine.AppliedTorque.y, 0f, 1f),
                        ShipThrust.YawLeft => Mathf.Clamp(0f - engine.AppliedTorque.y, 0f, 1f),
                        _ => 0f,
                    };
                }
                if (movementThrust > maximumThrust)
                {
                    maximumThrust = movementThrust;
                }
            }

            __result = maximumThrust;
            return false;
        }
    }
}
