using Gameplay.Ship;
using Gameplay.SpacePlatforms;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VCSpacePhysics.Ship.Physics
{
    [HarmonyPatch]
    public class ShipPhysicsPatches
    {
        public const float REARWARD_POWER_MULTIPLIER = 0.5f;
        public const float TORQUE_MULTIPLIER = 2f;

        // This patch (mostly) disables friction to the translation of the ship.
        // Friction for rotation is preserved. This is not realistic but is a concession to playability.
        [HarmonyPrefix, HarmonyPatch(typeof(MovingSpacePlatform), nameof(MovingSpacePlatform.ApplyFriction))]
        public static bool ApplyFriction(MovingSpacePlatform __instance, float deltaTime)
        {
            float num2 = (__instance.addingTorque ? __instance.angularVelocity.magnitude : Mathf.Max(__instance.angularVelocity.magnitude, __instance.PhysicalData.MinAngularVelocityFriction));
            float maxDistanceDelta2 = num2 * num2 * __instance.PhysicalData.AngularFriction * deltaTime / __instance.PhysicalData.Mass;
            __instance.angularVelocity = Vector3.MoveTowards(__instance.angularVelocity, Vector3.zero, maxDistanceDelta2);
            __instance.addingForce = (__instance.addingTorque = false);
            return false;
        }

        // Disable the ship's stupid autotilt. Seriously this visual effect makes no sense. It confused me a lot when I first played, and now it's gone gone GONE.
        [HarmonyPrefix, HarmonyPatch(typeof(ShipAutoTilt), nameof(ShipAutoTilt.ApplyPitch))]
        public static bool ApplyPitch()
        {
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(ShipAutoTilt), nameof(ShipAutoTilt.ApplyRoll))]
        public static bool ApplyRoll()
        {
            return false;
        }

        // The default ShipEngine.ApplyForce always applies movements along the default gameplay plane.
        // This patch allows forces to correctly be added relative to the ship's orientation when the ship has pitch/roll applied.
        [HarmonyPrefix, HarmonyPatch(typeof(ShipEngine), nameof(ShipEngine.ApplyForce))]
        public static bool ApplyForce(ShipEngine __instance)
        {
            if (__instance.IsPowered && __instance.photonView.AmOwner)
            {
                Vector3 a = __instance.InputSum(__instance.ThrustInputs);
                Vector3 a2 = ((!__instance.boosted) ? Vector3.Scale(a, __instance.EngineThrustPower) : Vector3.Scale(a, __instance.BoosterThrustPower));
                a2 = Vector3.Scale(a2, new Vector3(__instance.StrifePower.Value, __instance.ElevationPower.Value, __instance.ForwardPower.Value));
                if (a2.z < 0)
                {
                    a2 = Vector3.Scale(a2, new Vector3(1, 1, REARWARD_POWER_MULTIPLIER));
                }
                Vector3 vector = a2 * __instance.EnginePower.Value;
                __instance.ShipMovementController.AddForce(vector * Time.fixedDeltaTime, worldSpace: false, __instance.userInputsThrust || __instance.CruiseControlActive);
            }
            return false;
        }

        // The default ShipEngine.ApplyTorque calculation was not designed to apply power scaling for pitch and roll.
        // This patch applies torque equally along all three axis.
        [HarmonyPrefix, HarmonyPatch(typeof(ShipEngine), nameof(ShipEngine.ApplyTorque))]
        static bool ShipEngineApplyTorque(ShipEngine __instance)
        {
            if (__instance.IsPowered && __instance.photonView.AmOwner)
            {
                Vector3 a = __instance.InputSum(__instance.TorqueInputs);
                var appliedPower = !__instance.boosted ? __instance.EngineTorquePower.y : __instance.BoosterTorquePower.y;
                var torque = a * appliedPower * __instance.YawPower.Value * __instance.EnginePower.Value * TORQUE_MULTIPLIER;
                __instance.ShipMovementController.AddTorque(torque * Time.fixedDeltaTime, __instance.userInputsTorque);
            }

            return false;
        }


        // By default, the MovingSpacePlatform code applies some trickery to how angular momentum
        // is applied to rotation in order to ensure the ship will never rotate away from the gameplay plane.
        // This patch reworks the way angular momentum is applied so that the ship can rotate freely.
        [HarmonyPrefix, HarmonyPatch(typeof(MovingSpacePlatform), nameof(MovingSpacePlatform.FixedUpdate))]
        static bool MovingSpacePlatformFixedUpdate(MovingSpacePlatform __instance)
        {
            // NOTE: Calculations are applied to both Position/Rotation & CorrectedPosition/CorrectedRot
            // for netcode / prediction purposes. These calculations should be identical.

            var playerShip = __instance.gameObject.GetComponent<PlayerShip>();
            if (playerShip == null)
            {
                return true; // Not player ship, use default behaviour
            }

            __instance.PreMove();
            Vector3 position = __instance.SyncedTransform.position;
            Quaternion rotation = __instance.SyncedTransform.rotation;
            float fixedDeltaTime = Time.fixedDeltaTime;
            var rotationVector = __instance.AngularVelocity * fixedDeltaTime;
            Quaternion yawQuaternion = Quaternion.AngleAxis(rotationVector.y, __instance.SyncedTransform.up);
            Quaternion pitchQuaternion = Quaternion.AngleAxis(rotationVector.x, __instance.SyncedTransform.right);
            Quaternion rollQuaternion = Quaternion.AngleAxis(rotationVector.z, __instance.SyncedTransform.forward);
            var rotationQuaternion = rollQuaternion * pitchQuaternion * yawQuaternion; // Multiplication order is z x y (Euler rotation to quaternion)
            __instance.Rotation = rotationQuaternion * __instance.Rotation;
            __instance.CorrectedRot = rotationQuaternion * __instance.CorrectedRot;
            Vector3 vector = __instance.Velocity * fixedDeltaTime;
            __instance.ApplyFriction(fixedDeltaTime);
            __instance.Position += vector;
            __instance.CorrectedPosition += vector;
            __instance.Position = Vector3.MoveTowards(__instance.Position, __instance.CorrectedPosition, fixedDeltaTime * __instance.PositionCorrectionSpeed);
            __instance.Rotation = Quaternion.RotateTowards(__instance.Rotation, __instance.CorrectedRot, __instance.RotationCorrectionSpeed * fixedDeltaTime);
            __instance.positionDelta = __instance.Position - __instance.SyncedTransform.position;
            __instance.rotationDelta = Quaternion.Inverse(__instance.SyncedTransform.rotation) * __instance.Rotation;
            if (__instance.PositionDelta.magnitude > __instance.TeleportDistance + __instance.TeleportSpeedAdjustMult * __instance.Velocity.magnitude || Quaternion.Angle(__instance.CorrectedRot, __instance.SyncedTransform.rotation) > __instance.TeleportMaxAngleDelta)
            {
                __instance.positionDelta = __instance.CorrectedPosition - position;
                __instance.rotationDelta = Quaternion.Inverse(rotation) * __instance.CorrectedRot;
                __instance.Position = __instance.CorrectedPosition;
                __instance.Rotation = __instance.CorrectedRot;
            }
            __instance.Rotation = __instance.Rotation.normalized;
            __instance.syncedRigidBody.MoveRotation(__instance.Rotation);
            __instance.syncedRigidBody.MovePosition(__instance.Position);
            __instance.UpdateCorrectionSpeed();
            __instance.SimulatePhysicsScene();

            return false;
        }
    }
}
