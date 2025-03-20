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
        public static bool ApplyFriction(float deltaTime)
        {
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
            __instance.gameObject.AddComponent<HelmExtras>();

            var pitchYawUIGameObject = new GameObject();
            pitchYawUIGameObject.transform.SetParent(__instance.transform);
            pitchYawUIGameObject.transform.localPosition = new Vector3(0, 1.75f, -2.2f);
            pitchYawUIGameObject.AddComponent<PitchYawUI>();

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
            __instance.EngineTorquePower.x = __instance.EngineTorquePower.y = 15;
            __instance.EngineTorquePower.z = __instance.EngineTorquePower.y = 15;

            return true;
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

        [HarmonyPrefix, HarmonyPatch(typeof(CinemachineHardLookAt), nameof(CinemachineHardLookAt.MutateCameraState))]
        static void CinemachineHardLookAtMutateCameraState(CinemachineHardLookAt __instance, ref CameraState curState, float deltaTime)
        {
            curState.ReferenceUp = __instance.FollowTarget.transform.up;
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
        public Vector3 _rotateInputPos = Vector3.zero;
        public Vector3 _rotateInputNeg = Vector3.zero;
        private Helm _helm;

        public void Awake()
        {
            _helm = gameObject.GetComponent<Helm>();
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
    }

    class PitchYawUI : MonoBehaviour
    {
        static PitchYawUI _instance;
        Canvas canvas;

        GameObject outerCircle;
        GameObject innerCircle;

        public void Awake()
        {
            if(_instance != null)
            {
                Destroy(_instance.gameObject); // Only allow one at a time
            }
            _instance = this;

            var rectTransform = gameObject.AddComponent<RectTransform>();
            rectTransform.anchoredPosition3D = new Vector3(-0.0121f, 1.8051f, - 2.16f);
            rectTransform.sizeDelta = new Vector2(0.2f, 0.2f);

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            outerCircle = new GameObject();
            outerCircle.transform.SetParent(gameObject.transform);

            var outerCircleRectTransform = outerCircle.AddComponent<RectTransform>();
            outerCircleRectTransform.anchoredPosition3D = Vector3.zero;
            outerCircleRectTransform.sizeDelta = new Vector2(0.5f, 0.5f);

            var outerCircleCircle = outerCircle.AddComponent<Circle>();
            outerCircleCircle.color = Color.white;
            outerCircleCircle.lineWeight = 0.003f;
            outerCircleCircle.filled = false;
            outerCircleCircle.segments = 128;



            innerCircle = new GameObject();
            innerCircle.transform.SetParent(gameObject.transform);

            var innerCircleRectTransform = innerCircle.AddComponent<RectTransform>();
            innerCircleRectTransform.anchoredPosition3D = Vector3.zero;
            innerCircleRectTransform.sizeDelta = new Vector2(0.02f, 0.02f);

            var innerCircleCircle = innerCircle.AddComponent<Circle>();
            innerCircleCircle.color = Color.white;
            innerCircleCircle.filled = true;
            innerCircleCircle.segments = 12;
        }

        public void OnDestroy()
        {
            _instance = null;
        }
    }
}
