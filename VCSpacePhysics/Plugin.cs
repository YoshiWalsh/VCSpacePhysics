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

namespace VCSpacePhysics
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
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
            if (__instance.IsPowered && ((MonoBehaviourPun)__instance).photonView.AmOwner)
            {
                Quaternion quaternion = Quaternion.FromToRotation(Vector3.forward, Vector3.ProjectOnPlane(__instance.ShipMovementController.transform.forward, Vector3.up));
                Vector3 a = __instance.InputSum(__instance.ThrustInputs);
                Vector3 a2 = ((!__instance.boosted) ? Vector3.Scale(a, __instance.EngineThrustPower) : Vector3.Scale(a, __instance.BoosterThrustPower));
                a2 = Vector3.Scale(a2, new Vector3(__instance.StrifePower.Value, __instance.ElevationPower.Value, __instance.ForwardPower.Value));
                if (a2.z < 0)
                {
                    a2 = Vector3.Scale(a2, new Vector3(1, 1, REARWARD_POWER_MULTIPLIER));
                }
                Vector3 vector = quaternion * a2 * __instance.EnginePower.Value;
                __instance.ShipMovementController.AddForce(vector * Time.fixedDeltaTime, worldSpace: true, __instance.userInputsThrust || __instance.CruiseControlActive);
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

            foreach (SettingsControlList.Group group in __instance.KeyBindDisplayList().groups)
            {
                if (group.inputActionMapName == "Ship")
                {
                    group.Name = new DefaultableLocalizedString("Additional Controls (Ship / EVA Jetpack)", new LocalizedString());
                }
                if (group.Name.FallBackString == "Additional Controls (EVA Jetpack)")
                {
                    __instance.KeyBindDisplayList().groups.Remove(group);
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

                if (gameObject.transform.localPosition.x < 0 - THRUSTER_POSITION_EPSILON) // Left upwards thrusters
                {
                    thrusterMovements |= ShipThrust.RollClockwise;
                }
                if (gameObject.transform.localPosition.x > 0 + THRUSTER_POSITION_EPSILON) // Right upwards thrusters
                {
                    thrusterMovements |= ShipThrust.RollCounterclockwise;
                }
                if (gameObject.transform.localPosition.z < 0 - THRUSTER_POSITION_EPSILON) // Rear upwards thrusters
                {
                    thrusterMovements |= ShipThrust.PitchDown;
                }
                if (gameObject.transform.localPosition.z > 0 + THRUSTER_POSITION_EPSILON) // Front upwards thrusters
                {
                    thrusterMovements |= ShipThrust.PitchUp;
                }
            }
            if (__instance.flags.HasFlag(ThrusterEffectPlayerInput.ThrustFlags.Input_Down))
            {
                thrusterMovements |= ShipThrust.Down;

                if (gameObject.transform.localPosition.x < 0 - THRUSTER_POSITION_EPSILON) // Left downwards thrusters
                {
                    thrusterMovements |= ShipThrust.RollCounterclockwise;
                }
                if (gameObject.transform.localPosition.x > 0 + THRUSTER_POSITION_EPSILON) // Right downwards thrusters
                {
                    thrusterMovements |= ShipThrust.RollClockwise;
                }
                if (gameObject.transform.localPosition.z < 0 - THRUSTER_POSITION_EPSILON) // Rear downwards thrusters
                {
                    thrusterMovements |= ShipThrust.PitchUp;
                }
                if (gameObject.transform.localPosition.z > 0 + THRUSTER_POSITION_EPSILON) // Front downwards thrusters
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
                        ShipThrust.RollCounterclockwise => Mathf.Clamp(0f - engine.AppliedTorque.z, 0f, 1f),
                        ShipThrust.RollClockwise => Mathf.Clamp(engine.AppliedTorque.z, 0f, 1f),
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
}
