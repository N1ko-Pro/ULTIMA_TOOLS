using System.Collections;
using System.Reflection;
using MSCLoader;

namespace UltimaLoc
{
    /// <summary>
    /// Translates MSCLoader settings that were already materialized at load time.
    ///
    /// Why this is needed in addition to the transpiler: a mod's settings UI text
    /// (headers, checkbox/slider labels, dropdown items) are passed as string
    /// literals to Settings.Add* during the mod's settings-creation pass, which
    /// MSCLoader runs at LOAD time — before our OnMenuLoad patch. By then the
    /// literals are stored as data in ModSetting objects, so transpiling the
    /// (already-executed) creation method can't change them. Here we walk the
    /// finished ModSetting objects and translate their stored text directly,
    /// looking each string up by the shared id contract.
    ///
    /// Uses reflection for MSCLoader's internal members so it stays decoupled
    /// from their access level. Never throws.
    /// </summary>
    internal static class LocSettings
    {
        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly FieldInfo F_settingsList = typeof(Mod).GetField("modSettingsList", MemberFlags);
        private static readonly FieldInfo F_name = typeof(ModSetting).GetField("Name", MemberFlags);
        private static readonly MethodInfo M_updateName = typeof(ModSetting).GetMethod("UpdateName", MemberFlags);

        /// <summary>Translate all loaded mods' settings + descriptions. Returns count changed.</summary>
        public static int TranslateLoadedSettings()
        {
            int changed = 0;

            System.Collections.Generic.List<Mod> mods;
            try { mods = ModLoader.LoadedMods; }
            catch { return 0; }
            if (mods == null) return 0;

            foreach (Mod mod in mods)
            {
                if (mod == null) continue;

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
                    if (list == null) continue;
                    foreach (object setting in list)
                    {
                        changed += TranslateSetting(setting);
                    }
                }
                catch { /* ignore */ }
            }
            return changed;
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
