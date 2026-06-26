using System.IO;
using System.Reflection;
using Harmony;
using MSCLoader;

namespace UltimaLoc
{
    /// <summary>
    /// ULTIMA universal localization patcher — an MSCLoader mod that translates
    /// OTHER mods' hardcoded string literals at runtime from translation tables
    /// shipped next to it (Mods/Config/UltimaLoc/*.json), without replacing any
    /// original .dll.
    /// </summary>
    public class UltimaLocMod : Mod
    {
        public override string ID => "UltimaLocPatcher";
        public override string Name => "ULTIMA Localization Patcher";
        public override string Version => "1.0.2";
        public override string Author => "ANICKON";

        public UltimaLocMod()
        {
            Description = "Translates other mods' strings at runtime from ULTIMA translation tables. " +
                          "Does not modify the original mod files.";
        }

        public override void ModSetup()
        {
            // OnMenuLoad is the earliest stable phase where every mod assembly is
            // already loaded into the AppDomain — patch before gameplay JITs the
            // target methods.
            SetupFunction(Setup.OnMenuLoad, Mod_OnMenuLoad);
        }

        private void Mod_OnMenuLoad()
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string configDir = Path.Combine(Path.Combine(dir, "Config"), "UltimaLoc");

                int tables = LocStore.LoadFromDirectory(configDir);
                if (tables == 0 || LocStore.Targets.Count == 0)
                {
                    ModConsole.Log("[ULTIMA Loc] No translation tables found — nothing to patch.");
                    return;
                }

                Harmony.HarmonyInstance harmony = Harmony.HarmonyInstance.Create("ultima.loc.patcher");
                int patched = LocPatch.ApplyToLoadedTargets(harmony);

                // Translate settings/descriptions that MSCLoader already
                // materialized at load time (the transpiler can't reach those).
                int settings = LocSettings.TranslateLoadedSettings();

                ModConsole.Log(string.Format(
                    "[ULTIMA Loc] Loaded {0} table(s), {1} string(s); patched {2} method(s) across {3} target assembly(ies); translated {4} setting(s).",
                    tables, LocStore.Map.Count, patched, LocStore.Targets.Count, settings));
            }
            catch (System.Exception ex)
            {
                // Never break the game / other mods on a patcher failure.
                ModConsole.Error("[ULTIMA Loc] Patcher failed: " + ex.Message);
            }
        }
    }
}
