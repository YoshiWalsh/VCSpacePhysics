using CG.Ship.Modules;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VCSpacePhysics.Ship.VoidJump
{
    public class VoidJumpPatches
    {
        // In order for the ship to complete a void jump, it needs to be aimed at the exit vector.
        // The default code to automatically rotate the ship towards the correct alignment
        // was only designed to work around one axis.
        // This patch will rotate the ship towards a point around two axes.

        // TODO: This doesn't take into account angular momentum, so if the ship is already
        // pitching/yawing when the alignment process starts the alignment might overshoot
        // and take a long time spiralling in to the correct point.
        [HarmonyPrefix, HarmonyPatch(typeof(Helm), nameof(Helm.RotateTowardsPerFrame))]
        static bool HelmRotateTowardsPerFrame(Helm __instance, out bool __result, Vector3 pos)
        {
            var relativeDirection = __instance.Ship.gameObject.transform.InverseTransformDirection(pos).normalized; // Work in ship's local space instead of world space
            Vector2 totalYawAndPitchAngle = Vector3.ProjectOnPlane(relativeDirection, Vector3.forward) * 90f; // Find the direction and distance to the destination in 2D space (pitch/yaw)
            if (relativeDirection.z < 0f) // If the destination is directly behind the ship, the projected will be low but we should turn fast
            {
                if (totalYawAndPitchAngle.magnitude == 0f)
                {
                    totalYawAndPitchAngle.y = 180f; // Check for if the destination is literally exactly behind the ship (default to pitching up)
                }
                else
                {
                    totalYawAndPitchAngle = totalYawAndPitchAngle.normalized * (180f - totalYawAndPitchAngle.magnitude);
                }
            }
            var yawAndPitchInput = totalYawAndPitchAngle.normalized * Mathf.Min(totalYawAndPitchAngle.magnitude / 30f, 1f);

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
            Vector3 torque = new Vector3(-yawAndPitchInput.y, yawAndPitchInput.x, 0f);
            __instance.Engine.SetInput(Vector3.zero, torque);
            __result = false;
            return false;
        }
    }
}
