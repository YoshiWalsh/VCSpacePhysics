using BepInEx;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using System;
using System.Collections.Generic;
using Gameplay.SpacePlatforms;
using Gameplay.Ship;
using Photon.Pun;
using CG.Input;
using UnityEngine.Localization;
using VFX;
using CG.Ship.Modules;
using UnityEngine.InputSystem;
using Gameplay.Helm;
using Cinemachine;
using UnityEngine.UI;
using BepInEx.Logging;
using System.Diagnostics.CodeAnalysis;
using CG.Client;
using Opsive.UltimateCharacterController.Character.Identifiers;
using CG.Client.Player;
using CG.Game.Player;
using UnityEngine.UIElements;
using FMODUnity;
using Cinemachine.Utility;
#pragma warning disable CS0612 // Suppress obsolete warning
using static VFX.ThrusterEffectPlayerInput;

namespace VCSpacePhysics
{
    [HarmonyPatch]
    public class Patches
    {
        const float REARWARD_POWER_MULTIPLIER = 0.5f;

        // Remove default (bad) behaviour
        [HarmonyPrefix, HarmonyPatch(typeof(MovingSpacePlatform), nameof(MovingSpacePlatform.ApplyFriction))]
        public static bool ApplyFriction(MovingSpacePlatform __instance, float deltaTime)
        {
            float num2 = (__instance.addingTorque ? __instance.angularVelocity.magnitude : Mathf.Max(__instance.angularVelocity.magnitude, __instance.PhysicalData.MinAngularVelocityFriction));
            float maxDistanceDelta2 = num2 * num2 * __instance.PhysicalData.AngularFriction * deltaTime / __instance.PhysicalData.Mass;
            __instance.angularVelocity = Vector3.MoveTowards(__instance.angularVelocity, Vector3.zero, maxDistanceDelta2);
            __instance.addingForce = (__instance.addingTorque = false);
            return false;
        }

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

        [HarmonyPrefix, HarmonyPatch(typeof(InputKeyBinding), nameof(InputKeyBinding.RebuildKeybindArray), [])]
        public static void RebuildKeybindArray(InputKeyBinding __instance)
        {
            var ControlRenames = new Dictionary<string, Dictionary<string, DefaultableLocalizedString>>
            {
                ["Ship"] = new Dictionary<string, DefaultableLocalizedString>
                {
                    ["Move Forward"] = new DefaultableLocalizedString("Thrust Forward", new LocalizedString()),
                    ["Move Backward"] = new DefaultableLocalizedString("Thrust Backward", new LocalizedString()),
                    ["Rotate Left"] = new DefaultableLocalizedString("Thrust Left", new LocalizedString()),
                    ["Rotate Right"] = new DefaultableLocalizedString("Thrust Right", new LocalizedString()),
                    ["Move Up"] = new DefaultableLocalizedString("Thrust Up", new LocalizedString()),
                    ["Move Down"] = new DefaultableLocalizedString("Thrust Down", new LocalizedString()),
                    ["Move Left"] = new DefaultableLocalizedString("Roll Counter-clockwise", new LocalizedString()),
                    ["Move Right"] = new DefaultableLocalizedString("Roll Clockwise", new LocalizedString()),
                    ["Thruster Boost"] = new DefaultableLocalizedString("Dash / Boost", new LocalizedString()),
                }
            };


            var keybinds = __instance.KeyBindDisplayList();
            var jetpackControlGroupIndex = keybinds.groups.FindIndex(g => g.Name.FallBackString == "Additional Controls (EVA Jetpack)");
            if(jetpackControlGroupIndex != -1)
            {
                keybinds.groups.RemoveAt(jetpackControlGroupIndex);
            }
            foreach (SettingsControlList.Group group in keybinds.groups)
            {
                if (group.inputActionMapName == "Ship")
                {
                    group.Name = new DefaultableLocalizedString("Additional Controls (Ship / EVA Jetpack)", new LocalizedString());
                }

                if (ControlRenames.ContainsKey(group.inputActionMapName))
                {
                    var groupEntriesMap = ControlRenames.GetValueOrDefault(group.inputActionMapName);
                    foreach (SettingsControlList.InputEntry entry in group.InputEntrys)
                    {
                        entry.Name = groupEntriesMap.GetValueOrDefault(entry.Name.FallBackString, entry.Name);
                    }
                }
            }
        }

        [Flags]
        public enum ShipThrust // Represent all possible 6DOF movements
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

        public static Dictionary<ThrusterEffectPlayerInput, ShipThrust> ThrusterMapping = new Dictionary<ThrusterEffectPlayerInput, ShipThrust>();

        [HarmonyPostfix, HarmonyPatch(typeof(ThrusterEffectPlayerInput), nameof(ThrusterEffectPlayerInput.Awake), [])]
        static void ThrusterEffectPlayerInputAwake(ThrusterEffectPlayerInput __instance)
        {
            const float THRUSTER_POSITION_EPSILON = 0.5f;
            GameObject gameObject = __instance.gameObject;
            ShipThrust thrusterMovements = ShipThrust.None;

            __instance.PowerOnLerp = 0.2f;
            __instance.PowerOffLerp = 0.2f;

            var Ship = __instance.gameObject.GetComponentInParent<PlayerShip>();
            if (Ship != null)
            {
                var thrusterPositionInShipSpace = Ship.gameObject.transform.InverseTransformPoint(gameObject.transform.position);

                if (__instance.flags.HasFlag(ThrustFlags.Input_Forward))
                {
                    thrusterMovements |= ShipThrust.Forward;
                }
                if (__instance.flags.HasFlag(ThrustFlags.Input_Back))
                {
                    thrusterMovements |= ShipThrust.Backward;
                }
                if (__instance.flags.HasFlag(ThrustFlags.Input_StrafeLeft))
                {
                    thrusterMovements |= ShipThrust.Left;
                }
                if (__instance.flags.HasFlag(ThrustFlags.Input_StrafeRight))
                {
                    thrusterMovements |= ShipThrust.Right;
                }
                if (__instance.flags.HasFlag(ThrustFlags.Input_Up))
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
                if (__instance.flags.HasFlag(ThrustFlags.Input_Down))
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
                if (__instance.flags.HasFlag(ThrustFlags.Input_TurnLeft))
                {
                    thrusterMovements |= ShipThrust.YawLeft;
                }
                if (__instance.flags.HasFlag(ThrustFlags.Input_TurnRight))
                {
                    thrusterMovements |= ShipThrust.YawRight;
                }
            }

            ThrusterMapping.Add(__instance, thrusterMovements);
            gameObject.AddComponent<ThrusterCleanupBehaviour>();
        }

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
                if(movementThrust > maximumThrust)
                {
                    maximumThrust = movementThrust;
                }
            }
            
            __result = maximumThrust;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Helm), nameof(Helm.Awake))]
        static bool HelmAwake(Helm __instance)
        {
            var helmExtras = __instance.gameObject.AddComponent<HelmExtras>();

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

        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.MoveLeft))]
        // Remap move left to roll counter clockwise
        static bool MoveLeft(ControllingHelm __instance, ref InputAction.CallbackContext obj)
        {
            var helmExtras = __instance._helm.gameObject.GetComponent<HelmExtras>();

            helmExtras._rotateInputNeg.z = obj.action.ReadValue<float>();
            helmExtras.SetRotationInput(helmExtras._rotateInputNeg - helmExtras._rotateInputPos);

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.MoveRight))]
        // Remap move right to roll clockwise
        static bool MoveRight(ControllingHelm __instance, ref InputAction.CallbackContext obj)
        {

            var helmExtras = __instance._helm.gameObject.GetComponent<HelmExtras>();

            helmExtras._rotateInputPos.z = obj.action.ReadValue<float>();
            helmExtras.SetRotationInput(helmExtras._rotateInputNeg - helmExtras._rotateInputPos);

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.RotateLeft))]
        // Remap rotate left to thrust left
        static bool RotateLeft(ControllingHelm __instance, ref InputAction.CallbackContext obj)
        {
            __instance._helmInputNeg.x = obj.action.ReadValue<float>();
            __instance._helm.SetTranslationInput(__instance._helmInputPos - __instance._helmInputNeg);

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.RotateRight))]
        // Remap rotate right to thrust right
        static bool RotateRight(ControllingHelm __instance, ref InputAction.CallbackContext obj)
        {
            __instance._helmInputPos.x = obj.action.ReadValue<float>();
            __instance._helm.SetTranslationInput(__instance._helmInputPos - __instance._helmInputNeg);

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ShipEngine), nameof(ShipEngine.ApplyTorque))]
        static bool ShipEngineApplyTorque(ShipEngine __instance)
        {
            if (__instance.IsPowered && __instance.photonView.AmOwner)
            {
                Vector3 a = __instance.InputSum(__instance.TorqueInputs);
                var appliedPower = !__instance.boosted ? __instance.EngineTorquePower.y : __instance.BoosterTorquePower.y;
                var torque = a * appliedPower * __instance.YawPower.Value * __instance.EnginePower.Value;
                __instance.ShipMovementController.AddTorque(torque * Time.fixedDeltaTime, __instance.userInputsTorque);
            }

            return false;
        }

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

        [HarmonyPostfix, HarmonyPatch(typeof(ShipExternalCamera), nameof(ShipExternalCamera.OnShipExternalViewToggle))]
        static void ShipExternalCameraOnShipExternalViewToggle(ShipExternalCamera __instance)
        {
            __instance.transform.rotation = __instance.transform.parent.rotation;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(CinemachineHardLookAt), nameof(CinemachineHardLookAt.MutateCameraState))]
        static void CinemachineHardLookAtMutateCameraState(CinemachineHardLookAt __instance, ref CameraState curState, float deltaTime)
        {
            curState.ReferenceUp = __instance.FollowTarget.transform.up;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(PlayerRootLocalCamera), nameof(PlayerRootLocalCamera.Awake))]
        static void PlayerRootLocalCameraAwake(PlayerRootLocalCamera __instance)
        {
            HelmExtras.playerFirstPersonCamera = __instance.gameObject;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.EnableInput))]
        static void ControllingHelmEnableInput(ControllingHelm __instance)
        {
            if(__instance._localPlayer.IsMine)
            {
                var helmExtras = __instance._helm.gameObject.GetComponent<HelmExtras>();
                __instance.InputActionReferences.Fire1.action.performed += helmExtras.ToggleYawPitch;
                __instance.InputActionReferences.Fire1.action.canceled += helmExtras.ToggleYawPitch;
                helmExtras.DisableYawPitch();
            }
        }

        private static FieldInfo inputSubscribedinfo = AccessTools.Field(typeof(ControllingHelm), nameof(ControllingHelm.DisableInput));
        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.DisableInput))]
        static void ControllingHelmDisableInput(ControllingHelm __instance)
        {
            if (__instance._localPlayer.IsMine && (bool)inputSubscribedinfo.GetValue(__instance))
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

        [HarmonyPrefix, HarmonyPatch(typeof(MovingSpacePlatform), nameof(MovingSpacePlatform.FixedUpdate))]
        static bool MovingSpacePlatformFixedUpdate(MovingSpacePlatform __instance)
        {
            var playerShip = __instance.gameObject.GetComponent<PlayerShip>();
            if(playerShip == null)
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

        [HarmonyPrefix, HarmonyPatch(typeof(PlayerShipMovementAudio), nameof(PlayerShipMovementAudio.Update))]
        static bool PlayerShipMovementAudioUpdate(PlayerShipMovementAudio __instance)
        {
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Helm), nameof(Helm.RotateTowardsPerFrame))]
        static bool HelmRotateTowardsPerFrame(Helm __instance, out bool __result, Vector3 pos)
        {
            var relativeDirection = __instance.Ship.gameObject.transform.InverseTransformDirection(pos).normalized; // Work in ship's local space instead of world space
            Vector2 totalYawAndPitchAngle = relativeDirection.ProjectOntoPlane(Vector3.forward) * 90f; // Find the direction and distance to the destination in 2D space (pitch/yaw)
            if(relativeDirection.z < 0f) // If the destination is directly behind the ship, the projected will be low but we should turn fast
            {
                if(totalYawAndPitchAngle.magnitude == 0f)
                {
                    totalYawAndPitchAngle.y = 180f; // Check for if the destination is literally exactly behind the ship (default to pitching up)
                } else
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
    class ThrusterCleanupBehaviour : MonoBehaviour
    {
        private ThrusterEffectPlayerInput thrusterEffect;

        public virtual void Awake()
        {
            thrusterEffect = this.gameObject.GetComponent<ThrusterEffectPlayerInput>();
        }

        public void OnDestroy()
        {
            Patches.ThrusterMapping.Remove(thrusterEffect);
        }
    }

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
            if(thirdPerson)
            {
                var thirdPersonUI = YawPitchUI._instances.Find(i => i.type == YawPitchUI.YawPitchUIType.Spatial);
                thirdPersonUI.helmExtras = this;
            }
        }

        public void FixedUpdate()
        {
            if(_helm._pilotingLocked)
            {
                var torque = _helm.Engine.PlayerInputTorque;
                rawYawPitch = new Vector2(torque.y, -torque.x);
                yawPitchInput = rawYawPitch.magnitude < 1f ? rawYawPitch : rawYawPitch.normalized;
                return;
            }

            if(!thirdPerson)
            {
                firstPersonUIPlayerLooking = GetYawPitchUILookingPoint();
            }
            if(controllingYawPitch)
            {
                if (thirdPerson)
                {
                    rawYawPitch += _helm._mouseDelta * mouseMagnitudeScaling;
                    if (rawYawPitch.magnitude > maximumYawPitchMagnitude)
                    {
                        rawYawPitch = rawYawPitch.normalized * maximumYawPitchMagnitude;
                    }
                } else
                {
                    
                    if(firstPersonUIPlayerLooking != null)
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
            if(clicked < 0.5f)
            {
                DisableYawPitch();
            } else
            {
                TryEnableYawPitch();
            }
        }

        public void TryEnableYawPitch()
        {
            if(controllingYawPitch)
            {
                return;
            }

            if(!thirdPerson)
            {
                if(firstPersonUIPlayerLooking == null || ((Vector2)firstPersonUIPlayerLooking).magnitude > 1f)
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

    class YawPitchUI : MonoBehaviour
    {
        public enum YawPitchUIType
        {
            Spatial,
            Camera
        }

        public static List<YawPitchUI> _instances = new List<YawPitchUI>();

        public YawPitchUIType type = YawPitchUIType.Spatial;

        public HelmExtras helmExtras;
        
        private Canvas canvas;

        private GameObject outerCircle;
        private GameObject innerCircle;

        public bool IsVisible;

        const float outerCircleDiameter = 0.5f;

        public void Awake()
        {
            _instances.Add(this);

            var rectTransform = gameObject.AddComponent<RectTransform>();
            rectTransform.anchoredPosition3D = new Vector3(-0.0121f, 1.8051f, - 2.16f);
            rectTransform.sizeDelta = new Vector2(0.2f, 0.2f);

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            outerCircle = new GameObject();
            outerCircle.transform.SetParent(gameObject.transform);

            var outerCircleRectTransform = outerCircle.AddComponent<RectTransform>();
            outerCircleRectTransform.anchoredPosition3D = Vector3.zero;
            outerCircleRectTransform.sizeDelta = new Vector2(outerCircleDiameter, outerCircleDiameter);

            var outerCircleCircle = outerCircle.AddComponent<Circle>();
            outerCircleCircle.color = Color.white;
            outerCircleCircle.lineWeight = 0.003f;
            outerCircleCircle.filled = false;
            outerCircleCircle.segments = 128;



            innerCircle = new GameObject();
            innerCircle.transform.SetParent(gameObject.transform);

            var innerCircleRectTransform = innerCircle.AddComponent<RectTransform>();
            innerCircleRectTransform.anchoredPosition3D = Vector2.zero;
            innerCircleRectTransform.sizeDelta = new Vector2(0.02f, 0.02f);

            var innerCircleCircle = innerCircle.AddComponent<Circle>();
            innerCircleCircle.color = Color.white;
            innerCircleCircle.filled = true;
            innerCircleCircle.segments = 12;
        }

        public void Update()
        {
            if(helmExtras == null)
            {
                return;
            }

            switch (type)
            {
                case YawPitchUIType.Spatial:
                    IsVisible = helmExtras._helm.IsPowered;
                    gameObject.transform.localPosition = new Vector3(0, 1.75f, -2.2f); // Not necessary to do every frame at all, I just wanted this code in the same place as the bit below.
                    break;
                case YawPitchUIType.Camera:
                default:
                    IsVisible = helmExtras._helm.IsPowered && helmExtras.controllingYawPitch;
                    gameObject.transform.localPosition = new Vector3(0, 0, 1.075f); // For some reason this doesn't keep its initial position. Putting it back every frame is overkill but it's an easy fix.
                    break;
            }

            outerCircle.SetActive(IsVisible);
            innerCircle.SetActive(IsVisible);

            if (!IsVisible)
            {
                return;
            }

            ((RectTransform)innerCircle.transform).anchoredPosition = helmExtras.yawPitchInput * outerCircleDiameter / 2f;
        }

        public void OnDestroy()
        {
            _instances.Remove(this);
        }

        public Vector2? GetLookingPosition(Ray ray)
        {
            var canvasPlane = new Plane(canvas.transform.forward, canvas.transform.position);

            float hitDistance;
            if(canvasPlane.Raycast(ray, out hitDistance))
            {
                var hitLocationWorldspace = ray.GetPoint(hitDistance);
                var hitLocationCanvasspace = canvas.transform.InverseTransformPoint(hitLocationWorldspace);
                return hitLocationCanvasspace / (outerCircleDiameter / 2f);
            }
            return null;
        }
    }
}
#pragma warning restore CS0618