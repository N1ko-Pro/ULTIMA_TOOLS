using System.Collections;
using System.Reflection;
using Harmony;
using MSCLoader;

namespace UltimaLoc
{
    /// <summary>
    /// Translates MSCLoader settings text (headers, checkbox/slider labels,
    /// dropdown items, placeholders, mod descriptions, keybind names).
    ///
    /// Two passes, because mods create settings at different times:
    ///   1. An initial sweep at OnMenuLoad over every already-registered setting
    ///      (covers mods that build their settings during load).
    ///   2. A Harmony PREFIX on ModMenuView.ModSettingsList / KeyBindsList that
    ///      re-translates a mod's settings right BEFORE its page is built. This
    ///      is essential for mods that add settings LAZILY (e.g. SettingsText
    ///      descriptions created in a ModSettings callback fired only when the
    ///      user opens the page — after OnMenuLoad), which the one-shot sweep
    ///      would otherwise miss. Idempotent: an already-translated string has no
    ///      table entry under its translated form, so re-runs are no-ops.
    ///
    /// Uses reflection for MSCLoader's internal members so it stays decoupled
    /// from their access level. Never throws.
    /// </summary>
    internal static class LocSettings
    {
        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly FieldInfo F_settingsList = typeof(Mod).GetField("modSettingsList", MemberFlags);
        private static readonly FieldInfo F_keybindsList = typeof(Mod).GetField("modKeybindsList", MemberFlags);
        private static readonly FieldInfo F_name = typeof(ModSetting).GetField("Name", MemberFlags);
        private static readonly FieldInfo F_keybindName = typeof(ModKeybind).GetField("Name", MemberFlags);
        private static readonly MethodInfo M_updateName = typeof(ModSetting).GetMethod("UpdateName", MemberFlags);

        /// <summary>
        /// Patch the settings-page builders so each mod's settings are translated
        /// just before its page is rendered (catches lazily-added settings).
        /// Returns the number of builder methods hooked (0 if unavailable).
        /// </summary>
        public static int Install(HarmonyInstance harmony)
        {
            if (harmony == null) return 0;
            try
            {
                System.Type tView = typeof(Mod).Assembly.GetType("MSCLoader.ModMenuView");
                if (tView == null) return 0;

                MethodInfo prefix = typeof(LocSettings).GetMethod(
                    "Prefix_TranslateMod", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefix == null) return 0;
                HarmonyMethod hmPrefix = new HarmonyMethod(prefix);

                int hooked = 0;
                foreach (string name in new[] { "ModSettingsList", "KeyBindsList" })
                {
                    MethodInfo m = tView.GetMethod(
                        name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (m == null) continue;
                    try { harmony.Patch(m, hmPrefix, null); hooked++; }
                    catch { /* skip a builder we couldn't patch */ }
                }
                return hooked;
            }
            catch { return 0; }
        }

        // Harmony prefix: both builders take a `mod` argument — translate its
        // settings before the page UI reads their Name fields.
        private static void Prefix_TranslateMod(Mod mod)
        {
            try { if (mod != null) TranslateMod(mod); }
            catch { /* never break the settings menu */ }
        }

        /// <summary>Initial sweep over all loaded mods. Returns count changed.</summary>
        public static int TranslateLoadedSettings()
        {
            int changed = 0;

            System.Collections.Generic.List<Mod> mods;
            try { mods = ModLoader.LoadedMods; }
            catch { return 0; }
            if (mods == null) return 0;

            foreach (Mod mod in mods)
            {
                if (mod != null) changed += TranslateMod(mod);
            }
            return changed;
        }

        /// <summary>Translate one mod's description + settings + keybinds.</summary>
        private static int TranslateMod(Mod mod)
        {
            int changed = 0;

            // Mod description shown on the mod card (a constructor-set field,
            // so the transpiler can't reach it — set it directly).
            try
            {
                string desc = mod.Description;
                string tr;
                if (!string.IsNullOrEmpty(desc) && LocStore.TryTranslate(desc, out tr))
                {
                    mod.Description = tr;
                    changed++;
                }
            }
            catch { /* ignore */ }

            // Settings labels / items.
            try
            {
                IEnumerable list = F_settingsList != null ? F_settingsList.GetValue(mod) as IEnumerable : null;
                if (list != null)
                {
                    foreach (object setting in list)
                    {
                        changed += TranslateSetting(setting);
                    }
                }
            }
            catch { /* ignore */ }

            // Keybind names + keybind-section headers. These live in a separate
            // list (modKeybindsList) from regular settings, and the keybind UI
            // reads ModKeybind.Name when it builds each row.
            try
            {
                IEnumerable kbList = F_keybindsList != null ? F_keybindsList.GetValue(mod) as IEnumerable : null;
                if (kbList != null)
                {
                    foreach (object keybind in kbList)
                    {
                        changed += TranslateKeybind(keybind);
                    }
                }
            }
            catch { /* ignore */ }

            return changed;
        }

        private static int TranslateKeybind(object keybind)
        {
            if (keybind == null || F_keybindName == null) return 0;
            try
            {
                string name = F_keybindName.GetValue(keybind) as string;
                string tr;
                if (!string.IsNullOrEmpty(name) && LocStore.TryTranslate(name, out tr))
                {
                    F_keybindName.SetValue(keybind, tr);
                    return 1;
                }
            }
            catch { /* ignore */ }
            return 0;
        }

        private static int TranslateSetting(object setting)
        {
            if (setting == null) return 0;
            int changed = 0;

            // The label (ModSetting.Name) — covers headers, checkboxes, sliders,
            // buttons, text, textbox/dropdown labels. UpdateName also refreshes a
            // live UI element if one already exists.
            try
            {
                string name = F_name != null ? F_name.GetValue(setting) as string : null;
                string tr;
                if (!string.IsNullOrEmpty(name) && LocStore.TryTranslate(name, out tr))
                {
                    if (M_updateName != null) M_updateName.Invoke(setting, new object[] { tr });
                    else if (F_name != null) F_name.SetValue(setting, tr);
                    changed++;
                }
            }
            catch { /* ignore */ }

            // Value labels on specific setting types.
            changed += TranslateStringArrayField(setting, "ArrayOfItems"); // SettingsDropDownList
            changed += TranslateStringArrayField(setting, "TextValues");   // SettingsSliderInt
            changed += TranslateStringField(setting, "Placeholder");       // SettingsTextBox / TextArea
            return changed;
        }

        private static int TranslateStringArrayField(object setting, string fieldName)
        {
            try
            {
                FieldInfo f = setting.GetType().GetField(fieldName, MemberFlags);
                if (f == null) return 0;
                string[] arr = f.GetValue(setting) as string[];
                if (arr == null) return 0;

                int changed = 0;
                for (int i = 0; i < arr.Length; i++)
                {
                    string tr;
                    if (!string.IsNullOrEmpty(arr[i]) && LocStore.TryTranslate(arr[i], out tr))
                    {
                        arr[i] = tr;
                        changed++;
                    }
                }
                return changed;
            }
            catch { return 0; }
        }

        private static int TranslateStringField(object setting, string fieldName)
        {
            try
            {
                FieldInfo f = setting.GetType().GetField(fieldName, MemberFlags);
                if (f == null) return 0;
                string val = f.GetValue(setting) as string;
                string tr;
                if (!string.IsNullOrEmpty(val) && LocStore.TryTranslate(val, out tr))
                {
                    f.SetValue(setting, tr);
                    return 1;
                }
                return 0;
            }
            catch { return 0; }
        }
    }
}
