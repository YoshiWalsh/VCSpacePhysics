using CG.Ship.Modules;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VCSpacePhysics.Ship.Physics;
using VCSpacePhysics.Utils;

namespace VCSpacePhysics.Ship.VoidJump
{
    [HarmonyPatch]
    public class VoidJumpPatches
    {
        const float SQRT2 = 1.41421356237f;

        // In order for the ship to complete a void jump, it needs to be aimed at the exit vector.
        // The default code to automatically rotate the ship towards the correct alignment
        // was only designed to work around one axis.
        // This patch will rotate the ship towards a point around two axes.

        [HarmonyPrefix, HarmonyPatch(typeof(Helm), nameof(Helm.RotateTowardsPerFrame))]
        static bool HelmRotateTowardsPerFrame(Helm __instance, out bool __result, Vector3 pos)
        {
            var movingSpacePlatform = __instance.Ship.Platform;
            var engine = __instance.Engine;

            var relativeDirection = __instance.Ship.gameObject.transform.InverseTransformDirection(pos).normalized; // Work in ship's local space instead of world space
            Vector2 yawAndPitchDirection = Vector3.ProjectOnPlane(relativeDirection, Vector3.forward) * 90f; // Find the direction to the destination in 2D space (pitch/yaw)

            // If the destination is directly behind the ship, pitch up
            if (relativeDirection.z < 0f && yawAndPitchDirection.magnitude == 0f) 
            {
                yawAndPitchDirection.y = 1f;
            }

            var yawAndPitchMagnitude = Vector3.Angle(Vector3.forward, relativeDirection);

            var totalYawAndPitchAngle = yawAndPitchDirection.normalized * yawAndPitchMagnitude;

            Vector2 currentYawAndPitchSpeed = new Vector2(movingSpacePlatform.AngularVelocity.y, -movingSpacePlatform.AngularVelocity.x) * Time.fixedDeltaTime;
            float maxTorque = engine.EngineTorquePower.y * ShipPhysicsPatches.TORQUE_MULTIPLIER * engine.YawPower.Value * engine.EnginePower.Value * Time.fixedDeltaTime * Time.fixedDeltaTime / movingSpacePlatform.PhysicalData.Mass;

            Vector2 desiredYawAndPitchInput = Movement.ApproximateRequiredAcceleration(totalYawAndPitchAngle, currentYawAndPitchSpeed, maxTorque) / maxTorque;

            if (totalYawAndPitchAngle.magnitude <= 5f)
            {
                __instance.Engine.SetInput(Vector3.zero, Vector3.zero);
                __instance.SlowStop();
                __result = true;
                return false;
            }
            if (!__instance.IsPowered)
            {
                __result = false;
                return false;
            }
            Vector3 torque = new Vector3(-desiredYawAndPitchInput.y, desiredYawAndPitchInput.x, 0f);
            __instance.Engine.SetInput(Vector3.zero, torque);
            __result = false;
            return false;
        }
    }
}
