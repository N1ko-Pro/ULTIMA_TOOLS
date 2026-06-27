using System;
using System.Reflection;
using Harmony;
using MSCLoader;

namespace UltimaLoc
{
    /// <summary>
    /// Makes translated settings labels FIT their UI box.
    ///
    /// Problem: a translated label is often longer than the English original
    /// (e.g. RU is ~1.3–1.6× wider), and MSCLoader's settings rows give each
    /// label Text a fixed-width box → the tail gets clipped ("…на остро|ve").
    ///
    /// Fix: Harmony-postfix every <c>SettingsElement.Setup*</c> method (they run
    /// exactly when a row's Text is populated, for every mod, every page build)
    /// and reconfigure the row's Text components to:
    ///   • wrap horizontally instead of overflowing, and
    ///   • auto-shrink the font (best-fit) so even a long line stays fully
    ///     visible inside whatever box the prefab provides — capped at the
    ///     original font size so short labels never grow.
    ///
    /// Everything is done through reflection on UnityEngine.UI.Text so the
    /// patcher needs NO UnityEngine reference at build time (keeps CI clean and
    /// avoids shipping engine DLLs), staying consistent with the rest of the mod.
    /// Never throws — a layout tweak must never break the game or other mods.
    /// </summary>
    internal static class LocLayout
    {
        // Smallest font best-fit may shrink to. Keeps very long lines readable
        // while still guaranteeing they fit.
        private const int BestFitMinSize = 8;

        private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

        // Resolved lazily from the MSCLoader assembly on first install.
        private static FieldInfo F_settingName;
        private static FieldInfo F_value;
        private static FieldInfo F_placeholder;

        // UnityEngine.UI.Text members (reflected from the field type).
        private static PropertyInfo P_bestFit;
        private static PropertyInfo P_minSize;
        private static PropertyInfo P_maxSize;
        private static PropertyInfo P_fontSize;
        private static PropertyInfo P_hOverflow;
        private static PropertyInfo P_vOverflow;
        private static object V_wrap;       // HorizontalWrapMode.Wrap
        private static object V_truncate;   // VerticalWrapMode.Truncate

        private static bool resolved;

        /// <summary>
        /// Patch the settings-row builders so every label auto-fits. Returns the
        /// number of Setup* methods successfully hooked (0 if unavailable).
        /// </summary>
        public static int Install(HarmonyInstance harmony)
        {
            if (harmony == null) return 0;
            try
            {
                Type tElement = typeof(Mod).Assembly.GetType("MSCLoader.SettingsElement");
                if (tElement == null) return 0;

                F_settingName = tElement.GetField("settingName", PublicInstance);
                F_value = tElement.GetField("value", PublicInstance);
                F_placeholder = tElement.GetField("placeholder", PublicInstance);

                if (!ResolveTextMembers()) return 0;

                MethodInfo postfix = typeof(LocLayout).GetMethod(
                    "FitRow", BindingFlags.Static | BindingFlags.NonPublic);
                if (postfix == null) return 0;
                HarmonyMethod hmPostfix = new HarmonyMethod(postfix);

                string[] setupMethods =
                {
                    "SetupCheckbox", "SetupButton", "SetupSliderInt",
                    "SetupSlider", "SetupTextBox", "SetupTextArea",
                };

                int hooked = 0;
                foreach (string name in setupMethods)
                {
                    MethodInfo m = tElement.GetMethod(
                        name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (m == null) continue;
                    try
                    {
                        harmony.Patch(m, null, hmPostfix);
                        hooked++;
                    }
                    catch { /* skip a method we couldn't patch */ }
                }
                return hooked;
            }
            catch
            {
                return 0;
            }
        }

        // Resolve the Text property/enum members once, from the settingName field
        // type (UnityEngine.UI.Text), so we never need a compile-time reference.
        private static bool ResolveTextMembers()
        {
            if (resolved) return P_bestFit != null;
            resolved = true;

            if (F_settingName == null) return false;
            Type tText = F_settingName.FieldType;
            if (tText == null) return false;

            P_bestFit = tText.GetProperty("resizeTextForBestFit", PublicInstance);
            P_minSize = tText.GetProperty("resizeTextMinSize", PublicInstance);
            P_maxSize = tText.GetProperty("resizeTextMaxSize", PublicInstance);
            P_fontSize = tText.GetProperty("fontSize", PublicInstance);
            P_hOverflow = tText.GetProperty("horizontalOverflow", PublicInstance);
            P_vOverflow = tText.GetProperty("verticalOverflow", PublicInstance);

            try
            {
                if (P_hOverflow != null)
                    V_wrap = Enum.Parse(P_hOverflow.PropertyType, "Wrap");
                if (P_vOverflow != null)
                    V_truncate = Enum.Parse(P_vOverflow.PropertyType, "Truncate");
            }
            catch { /* enum names stable since Unity 5; ignore if absent */ }

            return P_bestFit != null;
        }

        // Harmony postfix: __instance is the SettingsElement just populated.
        private static void FitRow(object __instance)
        {
            if (__instance == null) return;
            try
            {
                if (F_settingName != null) FitText(F_settingName.GetValue(__instance));
                if (F_value != null) FitText(F_value.GetValue(__instance));
                if (F_placeholder != null) FitText(F_placeholder.GetValue(__instance));
            }
            catch { /* never break the UI */ }
        }

        // Reconfigure one UnityEngine.UI.Text to wrap + best-fit-shrink so its
        // (possibly translated, longer) content stays fully visible.
        private static void FitText(object text)
        {
            if (text == null) return;
            try
            {
                if (P_hOverflow != null && V_wrap != null) P_hOverflow.SetValue(text, V_wrap, null);
                if (P_vOverflow != null && V_truncate != null) P_vOverflow.SetValue(text, V_truncate, null);

                // Cap best-fit at the label's designed size so only oversized
                // (translated) lines shrink; short labels keep their look.
                if (P_fontSize != null && P_maxSize != null)
                {
                    int fs = (int)P_fontSize.GetValue(text, null);
                    if (fs > 0) P_maxSize.SetValue(text, fs, null);
                }
                if (P_minSize != null) P_minSize.SetValue(text, BestFitMinSize, null);
                if (P_bestFit != null) P_bestFit.SetValue(text, true, null);
            }
            catch { /* ignore a single uncooperative Text */ }
        }
    }
}
