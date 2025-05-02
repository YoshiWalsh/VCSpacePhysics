using System;
using System.Collections.Generic;
using System.Text;
using CG.Ship.Modules;
using HarmonyLib;
using UnityEngine.InputSystem;
using UnityEngine;
using CG.Client.Player;

namespace VCSpacePhysics.Ship.Controls
{
    [HarmonyPatch]
    public class ShipControlsPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Helm), nameof(Helm.Awake))]
        static bool HelmAwake(Helm __instance)
        {
            // Instantiate HelmExtras helper
            var helmExtras = __instance.gameObject.AddComponent<HelmExtras>();

            // Spawn UI objects
            var yawPitchUIGameObject = new GameObject();
            yawPitchUIGameObject.transform.SetParent(__instance.transform);
            var yawPitchUI = yawPitchUIGameObject.AddComponent<YawPitchUI>();
            yawPitchUI.helmExtras = helmExtras;

            var externalYawPitchUIGameObject = new GameObject();
            externalYawPitchUIGameObject.transform.SetParent(__instance.ExternalCamera.thirdPersonShipCamera.transform);
            var externalYawPitchUI = externalYawPitchUIGameObject.AddComponent<YawPitchUI>();
            externalYawPitchUI.type = YawPitchUI.YawPitchUIType.Camera;
            externalYawPitchUI.helmExtras = helmExtras;

            return true;
        }

        // Remap move left to roll counter clockwise
        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.MoveLeft))]
        static bool MoveLeft(ControllingHelm __instance, InputAction.CallbackContext obj)
        {
            var helmExtras = __instance._helm.gameObject.GetComponent<HelmExtras>();

            helmExtras._rotateInputNeg.z = obj.action.ReadValue<float>();
            helmExtras.SetRotationInput(helmExtras._rotateInputNeg - helmExtras._rotateInputPos);

            return false;
        }

        // Remap move right to roll clockwise
        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.MoveRight))]
        static bool MoveRight(ControllingHelm __instance, InputAction.CallbackContext obj)
        {

            var helmExtras = __instance._helm.gameObject.GetComponent<HelmExtras>();

            helmExtras._rotateInputPos.z = obj.action.ReadValue<float>();
            helmExtras.SetRotationInput(helmExtras._rotateInputNeg - helmExtras._rotateInputPos);

            return false;
        }

        // Remap rotate left to thrust left
        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.RotateLeft))]
        static bool RotateLeft(ControllingHelm __instance, InputAction.CallbackContext obj)
        {
            __instance._helmInputNeg.x = obj.action.ReadValue<float>();
            __instance._helm.SetTranslationInput(__instance._helmInputPos - __instance._helmInputNeg);

            return false;
        }

        // Remap rotate right to thrust right
        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.RotateRight))]
        static bool RotateRight(ControllingHelm __instance, InputAction.CallbackContext obj)
        {
            __instance._helmInputPos.x = obj.action.ReadValue<float>();
            __instance._helm.SetTranslationInput(__instance._helmInputPos - __instance._helmInputNeg);

            return false;
        }

        // Enable/disable yaw/pitch controls when the player sits in/stands from the pilot seat.
        [HarmonyPostfix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.EnableInput))]
        static void ControllingHelmEnableInput(ControllingHelm __instance)
        {
            if (__instance._localPlayer.IsMine)
            {
                var helmExtras = __instance._helm.gameObject.GetComponent<HelmExtras>();
                __instance.InputActionReferences.Fire1.action.performed += helmExtras.ToggleYawPitch;
                __instance.InputActionReferences.Fire1.action.canceled += helmExtras.ToggleYawPitch;
                helmExtras.DisableYawPitch();
            }
        }
        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.DisableInput))]
        static void ControllingHelmDisableInput(ControllingHelm __instance)
        {
            if (__instance._localPlayer.IsMine && __instance.inputSubscribed)
            {
                var helmExtras = __instance._helm.gameObject.GetComponent<HelmExtras>();
                if (helmExtras != null)
                {
                    __instance.InputActionReferences.Fire1.action.performed -= helmExtras.ToggleYawPitch;
                    __instance.InputActionReferences.Fire1.action.canceled -= helmExtras.ToggleYawPitch;
                    helmExtras.DisableYawPitch();
                }
            }
        }

        // In order for the pitch/yaw UI to work, we need to be able to track the local player's camera.
        // This patch is a lazy way to make sure we can easily get a reference to the camera.
        [HarmonyPostfix, HarmonyPatch(typeof(PlayerRootLocalCamera), nameof(PlayerRootLocalCamera.Awake))]
        static void PlayerRootLocalCameraAwake(PlayerRootLocalCamera __instance)
        {
            HelmExtras.playerFirstPersonCamera = __instance.gameObject;
        }

        // Since the 1.1.0 update, mouse movements are no longer stored in Helm._mouseDelta. Instead,
        // Helm._controllerDelta exclusively handles gamepad inputs. For this reason we need
        // to track mouse movements ourselves.
        // This function only deals with mouse movements, there's a separate
        // RotateExternalCameraController function that handles gamepad-driven rotation.
        [HarmonyPostfix, HarmonyPatch(typeof(Helm), nameof(Helm.RotateExternalCamera))]
        static void HelmRotateExternalCamera(Helm __instance, Vector2 delta)
        {
            var helmExtras = __instance.gameObject.GetComponent<HelmExtras>();
            helmExtras.AddMouseMovement(delta);
        }
    }
}
