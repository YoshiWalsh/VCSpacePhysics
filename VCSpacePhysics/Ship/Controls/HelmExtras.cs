using CG.Client;
using CG.Ship.Modules;
using Gameplay.Helm;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine;

namespace VCSpacePhysics.Ship.Controls
{
    class HelmExtras : MonoBehaviour
    {
        public static GameObject playerFirstPersonCamera;
        const float maximumYawPitchMagnitude = 1.2f;
        public Vector3 _rotateInputPos = Vector3.zero;
        public Vector3 _rotateInputNeg = Vector3.zero;
        public Helm _helm;
        public Vector2 rawYawPitch = Vector2.zero;
        public Vector2 yawPitchInput = Vector2.zero;
        private float mouseMagnitudeScaling = 0.03f;
        private bool thirdPerson = false;
        public bool controllingYawPitch = false;
        private Vector2? firstPersonUIPlayerLooking;

        public void Awake()
        {
            _helm = gameObject.GetComponent<Helm>();

            ViewEventBus.Instance.OnShipExternalViewToggle.Subscribe(OnShipExternalViewToggle);
            OnShipExternalViewToggle(ShipExternalCamera.CameraType.FirstPersonCamera);
        }

        public void OnDestroy()
        {
            if (!(ViewEventBus.Instance == null))
            {
                ViewEventBus.Instance?.OnShipExternalViewToggle.Unsubscribe(OnShipExternalViewToggle);
            }
        }

        private void OnShipExternalViewToggle(ShipExternalCamera.CameraType cameraType)
        {
            thirdPerson = cameraType == ShipExternalCamera.CameraType.ThirdPersonCamera;
            if (thirdPerson)
            {
                var thirdPersonUI = YawPitchUI._instances.Find(i => i.type == YawPitchUI.YawPitchUIType.Spatial);
                thirdPersonUI.helmExtras = this;
            }
        }

        public void FixedUpdate()
        {
            if (_helm._pilotingLocked)
            {
                var torque = _helm.Engine.PlayerInputTorque;
                rawYawPitch = new Vector2(torque.y, -torque.x);
                yawPitchInput = rawYawPitch.magnitude < 1f ? rawYawPitch : rawYawPitch.normalized;
                return;
            }

            if (!thirdPerson)
            {
                firstPersonUIPlayerLooking = GetYawPitchUILookingPoint();
            }
            if (controllingYawPitch)
            {
                if (thirdPerson)
                {
                    rawYawPitch += _helm._controllerDelta * mouseMagnitudeScaling;
                    if (rawYawPitch.magnitude > maximumYawPitchMagnitude)
                    {
                        rawYawPitch = rawYawPitch.normalized * maximumYawPitchMagnitude;
                    }
                }
                else
                {

                    if (firstPersonUIPlayerLooking != null)
                    {
                        rawYawPitch = (Vector2)firstPersonUIPlayerLooking;
                    }
                }
            }
            yawPitchInput = rawYawPitch.magnitude < 1f ? rawYawPitch : rawYawPitch.normalized;
            _rotateInputPos.x = yawPitchInput.y; // Vertical movements pitch the ship, which is applied around the x axis
            _rotateInputPos.y = -yawPitchInput.x; // Horizontal movements yaw the ship, which is applied around the y axis
            SetRotationInput(_rotateInputNeg - _rotateInputPos);
        }

        private Vector2? GetYawPitchUILookingPoint()
        {
            var yawPitchBridgeUI = YawPitchUI._instances.Find(i => i.type == YawPitchUI.YawPitchUIType.Spatial);
            var cameraRay = new Ray(playerFirstPersonCamera.transform.position, playerFirstPersonCamera.transform.forward);
            return yawPitchBridgeUI.GetLookingPosition(cameraRay);
        }

        public void SetRotationInput(Vector3 rotationInput)
        {
            if (_helm.IsPowered && !_helm._pilotingLocked)
            {
                if (_helm._cruiseControlActive && rotationInput.magnitude <= 0.05f && !_helm._cruiseControlOverrulingReady)
                {
                    _helm._cruiseControlOverrulingReady = true;
                }
                if (_helm._cruiseControlActive && rotationInput.magnitude > 0.25f && _helm._cruiseControlOverrulingReady)
                {
                    _helm.RequestRegainManualControl();
                }
                if (!_helm._cruiseControlActive)
                {
                    var userInputsTorque = rotationInput.magnitude > 0.01f;
                    _helm.Engine.TorqueInputs["PlayerInput"] = rotationInput;
                }
            }
        }

        public void ToggleYawPitch(InputAction.CallbackContext obj)
        {
            var clicked = obj.action.ReadValue<float>();
            if (clicked < 0.5f)
            {
                DisableYawPitch();
            }
            else
            {
                TryEnableYawPitch();
            }
        }

        public void TryEnableYawPitch()
        {
            if (controllingYawPitch)
            {
                return;
            }

            if (!thirdPerson)
            {
                if (firstPersonUIPlayerLooking == null || ((Vector2)firstPersonUIPlayerLooking).magnitude > 1f)
                {
                    return;
                }
            }

            controllingYawPitch = true;
        }

        public void DisableYawPitch()
        {
            controllingYawPitch = false;
            rawYawPitch = Vector2.zero;
        }
    }
}
