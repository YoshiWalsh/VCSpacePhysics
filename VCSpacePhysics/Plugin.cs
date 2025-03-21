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
using static VFX.ThrusterEffectPlayerInput;
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

namespace VCSpacePhysics
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource logger;

        private void Awake()
        {
            logger = Logger;

            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            // Configure Harmony
            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            var assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {

        }
    }

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
                if (__instance.flags.HasFlag(ThrusterEffectPlayerInput.ThrustFlags.Input_TurnLeft))
                {
                    thrusterMovements |= ShipThrust.YawLeft;
                }
                if (__instance.flags.HasFlag(ThrusterEffectPlayerInput.ThrustFlags.Input_TurnRight))
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

            var pitchYawUIGameObject = new GameObject();
            pitchYawUIGameObject.transform.SetParent(__instance.transform);
            var pitchYawUI = pitchYawUIGameObject.AddComponent<PitchYawUI>();
            pitchYawUI.helmExtras = helmExtras;

            var externalPitchYawUIGameObject = new GameObject();
            externalPitchYawUIGameObject.transform.SetParent(__instance.ExternalCamera.thirdPersonShipCamera.transform);
            var externalPitchYawUI = externalPitchYawUIGameObject.AddComponent<PitchYawUI>();
            externalPitchYawUI.type = PitchYawUI.PitchYawUIType.Camera;
            externalPitchYawUI.helmExtras = helmExtras;

            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.MoveLeft))]
        // Remap move left to roll counter clockwise
        static bool MoveLeft(ControllingHelm __instance, InputAction.CallbackContext obj)
        {
            var helmExtras = __instance._helm.gameObject.GetComponent<HelmExtras>();

            helmExtras._rotateInputNeg.z = obj.action.ReadValue<float>();
            helmExtras.SetRotationInput(helmExtras._rotateInputNeg - helmExtras._rotateInputPos);

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.MoveRight))]
        // Remap move right to roll clockwise
        static bool MoveRight(ControllingHelm __instance, InputAction.CallbackContext obj)
        {

            var helmExtras = __instance._helm.gameObject.GetComponent<HelmExtras>();

            helmExtras._rotateInputPos.z = obj.action.ReadValue<float>();
            helmExtras.SetRotationInput(helmExtras._rotateInputNeg - helmExtras._rotateInputPos);

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.RotateLeft))]
        // Remap rotate left to thrust left
        static bool RotateLeft(ControllingHelm __instance, InputAction.CallbackContext obj)
        {
            __instance._helmInputNeg.x = obj.action.ReadValue<float>();
            __instance._helm.SetTranslationInput(__instance._helmInputPos - __instance._helmInputNeg);

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ControllingHelm), nameof(ControllingHelm.RotateRight))]
        // Remap rotate right to thrust right
        static bool RotateRight(ControllingHelm __instance, InputAction.CallbackContext obj)
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
                //var torquePower = new Vector3(0.03f, __instance.EngineTorquePower.y, 0.03f);
                var torquePower = new Vector3(__instance.EngineTorquePower.y, __instance.EngineTorquePower.y, __instance.EngineTorquePower.y); // TODO Needs to use boost
                Vector3 a2 = ((!__instance.boosted) ? Vector3.Scale(a, torquePower) : Vector3.Scale(a, __instance.BoosterTorquePower));
                a2 = Vector3.Scale(a2, new Vector3(__instance.YawPower.Value, __instance.YawPower.Value, __instance.YawPower.Value)) * __instance.EnginePower.Value;
                //a2 = Vector3.Scale(a2, new Vector3(0.03f, __instance.YawPower.Value, 0.03f)) * __instance.EnginePower.Value;

                //var worldTorque = __instance.ShipMovementController.gameObject.transform.TransformDirection(a2);
                __instance.ShipMovementController.AddTorque(a2 * Time.fixedDeltaTime, __instance.userInputsTorque);
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
                __instance.InputActionReferences.Fire1.action.performed += helmExtras.TogglePitchYaw;
                __instance.InputActionReferences.Fire1.action.canceled += helmExtras.TogglePitchYaw;
                helmExtras.DisablePitchYaw();
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
                    __instance.InputActionReferences.Fire1.action.performed -= helmExtras.TogglePitchYaw;
                    __instance.InputActionReferences.Fire1.action.canceled -= helmExtras.TogglePitchYaw;
                    helmExtras.DisablePitchYaw();
                }
            }
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
        const float maximumPitchYawMagnitude = 1.2f;
        public Vector3 _rotateInputPos = Vector3.zero;
        public Vector3 _rotateInputNeg = Vector3.zero;
        public Helm _helm;
        public Vector2 rawPitchYaw = Vector2.zero;
        public Vector2 pitchYawInput = Vector2.zero;
        private float mouseMagnitudeScaling = 0.03f;
        private bool thirdPerson = false;
        public bool controllingPitchYaw = false;
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
                var thirdPersonUI = PitchYawUI._instances.Find(i => i.type == PitchYawUI.PitchYawUIType.Spatial);
                thirdPersonUI.helmExtras = this;
            }
        }

        public void FixedUpdate()
        {
            if(!thirdPerson)
            {
                firstPersonUIPlayerLooking = GetPitchYawUILookingPoint();
            }
            if(controllingPitchYaw)
            {
                if (thirdPerson)
                {
                    rawPitchYaw += _helm._mouseDelta * mouseMagnitudeScaling;
                    if (rawPitchYaw.magnitude > maximumPitchYawMagnitude)
                    {
                        rawPitchYaw = rawPitchYaw.normalized * maximumPitchYawMagnitude;
                    }
                } else
                {
                    
                    if(firstPersonUIPlayerLooking != null)
                    {
                        rawPitchYaw = (Vector2)firstPersonUIPlayerLooking;
                    }
                }
            }
            pitchYawInput = rawPitchYaw.magnitude < 1f ? rawPitchYaw : rawPitchYaw.normalized;
            _rotateInputPos.x = pitchYawInput.y; // Vertical movements pitch the ship, which is applied around the x axis
            _rotateInputPos.y = -pitchYawInput.x; // Horizontal movements yaw the ship, which is applied around the y axis
            SetRotationInput(_rotateInputNeg - _rotateInputPos);
        }

        private Vector2? GetPitchYawUILookingPoint()
        {
            var pitchYawBridgeUI = PitchYawUI._instances.Find(i => i.type == PitchYawUI.PitchYawUIType.Spatial);
            var cameraRay = new Ray(playerFirstPersonCamera.transform.position, playerFirstPersonCamera.transform.forward);
            return pitchYawBridgeUI.GetLookingPosition(cameraRay);
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

        public void TogglePitchYaw(InputAction.CallbackContext obj)
        {
            var clicked = obj.action.ReadValue<float>();
            if(clicked < 0.5f)
            {
                DisablePitchYaw();
            } else
            {
                TryEnablePitchYaw();
            }
        }

        public void TryEnablePitchYaw()
        {
            if(controllingPitchYaw)
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

            controllingPitchYaw = true;
        }

        public void DisablePitchYaw()
        {
            controllingPitchYaw = false;
            rawPitchYaw = Vector2.zero;
        }
    }

    class PitchYawUI : MonoBehaviour
    {
        public enum PitchYawUIType
        {
            Spatial,
            Camera
        }

        public static List<PitchYawUI> _instances = new List<PitchYawUI>();

        public PitchYawUIType type = PitchYawUIType.Spatial;

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
                case PitchYawUIType.Spatial:
                    IsVisible = helmExtras._helm.IsPowered;
                    gameObject.transform.localPosition = new Vector3(0, 1.75f, -2.2f); // Not necessary to do every frame at all, I just wanted this code in the same place as the bit below.
                    break;
                case PitchYawUIType.Camera:
                default:
                    IsVisible = helmExtras._helm.IsPowered && helmExtras.controllingPitchYaw;
                    gameObject.transform.localPosition = new Vector3(0, 0, 1.075f); // For some reason this doesn't keep its initial position. Putting it back every frame is overkill but it's an easy fix.
                    break;
            }

            outerCircle.SetActive(IsVisible);
            innerCircle.SetActive(IsVisible);

            if (!IsVisible)
            {
                return;
            }

            ((RectTransform)innerCircle.transform).anchoredPosition = helmExtras.pitchYawInput * outerCircleDiameter / 2f;
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
