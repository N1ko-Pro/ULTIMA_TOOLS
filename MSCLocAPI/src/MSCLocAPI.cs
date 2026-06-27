using System.IO;
using System.Reflection;
using Harmony;
using MSCLoader;

namespace UltimaLoc
{
    /// <summary>
    /// MSCLoc API — an MSCLoader mod that translates OTHER mods' hardcoded string
    /// literals at runtime from translation tables shipped next to it
    /// (Mods/Config/MSCLocAPI/*.json), without replacing any original .dll.
    /// </summary>
    public class MSCLocAPI : Mod
    {
        public override string ID => "MSCLocAPI";
        public override string Name => "MSCLoc API";
        public override string Version => "1.0.10";
        public override string Author => "ANICKON";

        public MSCLocAPI()
        {
            Description = "Translates other mods' strings at runtime from MSCLoc API translation tables. " +
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
                string configDir = Path.Combine(Path.Combine(dir, "Config"), "MSCLocAPI");

                int tables = LocStore.LoadFromDirectory(configDir);
                if (tables == 0 || LocStore.Targets.Count == 0)
                {
                    ModConsole.Log("[MSCLoc API] No translation tables found — nothing to patch.");
                    return;
                }

                Harmony.HarmonyInstance harmony = Harmony.HarmonyInstance.Create("mscloc.api");
                int patched = LocPatch.ApplyToLoadedTargets(harmony);

                // Translate settings/descriptions that MSCLoader already
                // materialized at load time (the transpiler can't reach those).
                int settings = LocSettings.TranslateLoadedSettings();

                // Also re-translate each mod's settings right before its page is
                // built, so lazily-added settings (e.g. SettingsText descriptions
                // created when the menu opens) are covered too.
                LocSettings.Install(harmony);

                // Make translated labels fit their box (RU/long text was being
                // clipped). Hooks SettingsElement.Setup* — runs when pages build.
                int fitHooks = LocLayout.Install(harmony);

                ModConsole.Log(string.Format(
                    "[MSCLoc API] Loaded {0} table(s), {1} string(s); patched {2} method(s) across {3} target assembly(ies); translated {4} setting(s); fit-hooks {5}.",
                    tables, LocStore.Map.Count, patched, LocStore.Targets.Count, settings, fitHooks));
            }
            catch (System.Exception ex)
            {
                // Never break the game / other mods on a patcher failure.
                ModConsole.Error("[MSCLoc API] Patcher failed: " + ex.Message);
            }
        }
    }
}
