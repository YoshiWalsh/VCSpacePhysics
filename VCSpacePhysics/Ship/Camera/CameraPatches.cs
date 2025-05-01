using Cinemachine;
using Gameplay.Helm;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VCSpacePhysics.Ship.Camera
{
    [HarmonyPatch]
    public class CameraPatches
    {
        // When the ship is moving, the camera shifts based on its acceleration in order to make the speed feel more apparent.
        // By default this shift is only applied along the default gameplay plane.
        // This patch ensures that this shift takes place relative to the ship's rotation.
        [HarmonyPrefix, HarmonyPatch(typeof(ShipExternalCamera), nameof(ShipExternalCamera.Move))]
        static bool ShipExternalCameraMove(ShipExternalCamera __instance)
        {
            if (__instance._currentCameraType == ShipExternalCamera.CameraType.ThirdPersonCamera)
            {
                Vector3 vector = __instance.engine.AppliedThrust * __instance.accelerationOffsetMultiplier;
                Vector3 worldspaceVector = __instance.engine.ShipMovementController.transform.TransformDirection(vector);
                __instance.accelerationOffset = Vector3.Lerp(__instance.accelerationOffset, worldspaceVector, Time.deltaTime * 3f);
                __instance.Anchor.transform.position = __instance.transform.position - __instance.accelerationOffset;
                __instance.Anchor.localRotation = Quaternion.Euler(__instance.EulerRotation.y, __instance.EulerRotation.x, 0f);
            }
            return false;
        }

        // By default when the external camera looks at the ship, even if the camera position and ship position are correct,
        // the camera will be rolled incorrectly in order to keep the camera will be kept upright based on global-up.
        // This patch makes the camera stay upright relative to the ship instead.
        [HarmonyPrefix, HarmonyPatch(typeof(CinemachineHardLookAt), nameof(CinemachineHardLookAt.MutateCameraState))]
        static void CinemachineHardLookAtMutateCameraState(CinemachineHardLookAt __instance, ref CameraState curState, float deltaTime)
        {
            curState.ReferenceUp = __instance.FollowTarget.transform.up;
        }

        // When the external camera is active it follows the rotation movements of the ship. But when you switch to it, by default
        // it will always use global-up for orientation. So if the ship has pitch/roll *before* you switch to external camera,
        // it will be at the wrong angle relative to the ship, and stay at that wrong angle while the ship rotates.
        // This patch syncs the external camera angle with the current ship rotation when switching to external camera.
        [HarmonyPostfix, HarmonyPatch(typeof(ShipExternalCamera), nameof(ShipExternalCamera.OnShipExternalViewToggle))]
        static void ShipExternalCameraOnShipExternalViewToggle(ShipExternalCamera __instance)
        {
            __instance.transform.rotation = __instance.transform.parent.rotation;
        }
    }
}
