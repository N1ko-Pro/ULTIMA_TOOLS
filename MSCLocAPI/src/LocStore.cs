using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UltimaLoc
{
    /// <summary>
    /// Merged translation state: id→text lookup plus the set of target assembly
    /// names to patch. Ids are content hashes, so merging tables from several
    /// mods is safe (identical source text → identical id → identical
    /// translation).
    ///
    /// This part is pure (no JSON/IO dependency) so it can be unit-tested on a
    /// modern runtime. File loading lives in the partial in LocStore.Io.cs.
    /// </summary>
    public static partial class LocStore
    {
        // Merged id → translated text across every loaded table.
        public static readonly Dictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.Ordinal);

        // Simple assembly names that have at least one translation table.
        public static readonly HashSet<string> Targets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// True (with the translation) when <paramref name="original"/> has a
        /// non-empty translation in the merged map; false leaves the original.
        /// </summary>
        public static bool TryTranslate(string original, out string translated)
        {
            translated = null;
            if (string.IsNullOrEmpty(original)) return false;

            string id = LocId.Make(original);
            return Map.TryGetValue(id, out translated) && !string.IsNullOrEmpty(translated);
        }

        // Splits on newlines while KEEPING the separators (capturing group), so
        // a translated block can be rejoined with its original line breaks.
        private static readonly Regex NewlineSplit = new Regex("(\r\n|\r|\n)");

        /// <summary>
        /// Like <see cref="TryTranslate"/>, but also handles multi-line text that
        /// a mod built at runtime by concatenating several literals with
        /// Environment.NewLine (e.g. Settings.AddText(string.Concat(lines))). The
        /// whole string has no table id, but each individual line does — so we
        /// translate line-by-line and rejoin. Returns true if anything changed.
        /// </summary>
        public static bool TryTranslateBlock(string original, out string translated)
        {
            translated = original;
            if (string.IsNullOrEmpty(original)) return false;

            // Fast path: the whole string is a known literal.
            string whole;
            if (TryTranslate(original, out whole)) { translated = whole; return true; }

            // No line breaks → nothing more we can do.
            if (original.IndexOf('\n') < 0 && original.IndexOf('\r') < 0) return false;

            // parts: even indices = line content, odd indices = separators.
            string[] parts = NewlineSplit.Split(original);
            bool any = false;
            for (int i = 0; i < parts.Length; i += 2)
            {
                string seg = parts[i];
                string tr;
                if (!string.IsNullOrEmpty(seg) && TryTranslate(seg, out tr))
                {
                    parts[i] = tr;
                    any = true;
                }
            }
            if (!any) return false;
            translated = string.Concat(parts);
            return true;
        }

        // Test/diagnostics helper — clears all loaded state.
        public static void Reset()
        {
            Map.Clear();
            Targets.Clear();
        }
    }
}
