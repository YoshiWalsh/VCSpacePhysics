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
using FMODUnity;
using Cinemachine.Utility;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.FirstPersonController.Camera.ViewTypes;
using Opsive.UltimateCharacterController.Character.Abilities;

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
            harmony.PatchAll();
        }
    }
}
