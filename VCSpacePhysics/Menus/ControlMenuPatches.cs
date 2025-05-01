using CG.Input;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Localization;

namespace VCSpacePhysics.Menus
{
    [HarmonyPatch]
    public class ControlMenuPatches
    {
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
            if (jetpackControlGroupIndex != -1)
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
    }
}
