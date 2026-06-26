using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace UltimaLoc
{
    /// <summary>
    /// File-loading half of LocStore (depends on Newtonsoft.Json, provided by
    /// MSCLoader at runtime). Kept separate from the pure half so the lookup
    /// logic can be unit-tested without a JSON dependency.
    /// </summary>
    public static partial class LocStore
    {
        /// <summary>
        /// Load every *.json table in <paramref name="dir"/>. Malformed files are
        /// skipped. Returns the number of tables successfully loaded.
        /// </summary>
        public static int LoadFromDirectory(string dir)
        {
            int loaded = 0;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;

            foreach (string file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    JObject root = JObject.Parse(File.ReadAllText(file));

                    string target = (string)root["targetAssembly"];
                    if (!string.IsNullOrEmpty(target)) Targets.Add(target);

                    JObject entries = root["entries"] as JObject;
                    if (entries != null)
                    {
                        foreach (KeyValuePair<string, JToken> kv in entries)
                        {
                            string value = (string)kv.Value;
                            if (!string.IsNullOrEmpty(kv.Key) && !string.IsNullOrEmpty(value))
                            {
                                Map[kv.Key] = value;
                            }
                        }
                    }
                    loaded++;
                }
                catch
                {
                    // Skip malformed/partial tables — never break other mods.
                }
            }
            return loaded;
        }
    }
}
