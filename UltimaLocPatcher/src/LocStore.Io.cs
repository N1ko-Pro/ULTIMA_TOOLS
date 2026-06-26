using System.Collections.Generic;
using System.IO;

namespace UltimaLoc
{
    /// <summary>
    /// File-loading half of LocStore. Uses the dependency-free MiniJson parser
    /// (NOT Newtonsoft.Json, which crashes on MSC's stripped Unity runtime).
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
                    var root = MiniJson.Parse(File.ReadAllText(file)) as Dictionary<string, object>;
                    if (root == null) continue;

                    object targetObj;
                    if (root.TryGetValue("targetAssembly", out targetObj) && targetObj is string)
                    {
                        string target = (string)targetObj;
                        if (!string.IsNullOrEmpty(target)) Targets.Add(target);
                    }

                    object entriesObj;
                    if (root.TryGetValue("entries", out entriesObj) && entriesObj is Dictionary<string, object>)
                    {
                        foreach (KeyValuePair<string, object> kv in (Dictionary<string, object>)entriesObj)
                        {
                            string value = kv.Value as string;
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
