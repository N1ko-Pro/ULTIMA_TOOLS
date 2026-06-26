using System;
using System.Collections.Generic;

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

        // Test/diagnostics helper — clears all loaded state.
        public static void Reset()
        {
            Map.Clear();
            Targets.Clear();
        }
    }
}
