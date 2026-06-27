using System;
using System.Net;
using System.Threading;

namespace UltimaLoc
{
    /// <summary>
    /// Lightweight, fail-silent update check for MSCLoc API.
    ///
    /// The patcher is distributed/installed by the ULTIMA app (downloaded from
    /// the ULTIMA_TOOLS GitHub release), so it isn't in MSCLoader's native mod
    /// catalogue and won't get the built-in "UPDATE AVAILABLE" banner. Instead
    /// we fetch a tiny `latest-version.txt` (written by the release script) on a
    /// background thread and expose whether a newer version exists. The mod then
    /// surfaces a notice in its settings page (UI work stays on the main thread).
    ///
    /// Never blocks the game and never throws: any network/TLS failure (MSC's
    /// Unity Mono can be picky about HTTPS) just leaves <see cref="UpdateAvailable"/>
    /// false — the app-side updater remains the reliable path.
    /// </summary>
    internal static class LocUpdate
    {
        private const string VersionUrl =
            "https://raw.githubusercontent.com/N1ko-Pro/ULTIMA_TOOLS/main/MSCLocAPI/latest-version.txt";

        private static volatile string latest;   // fetched latest version, or null
        private static string current;            // this build's version

        /// <summary>Start a non-blocking background version check.</summary>
        public static void CheckAsync(string currentVersion)
        {
            current = currentVersion;
            try { ThreadPool.QueueUserWorkItem(delegate { Fetch(); }); }
            catch { /* thread pool unavailable — skip silently */ }
        }

        private static void Fetch()
        {
            try
            {
                // Best-effort: prefer modern TLS where the runtime supports it.
                try
                {
                    ServicePointManager.SecurityProtocol =
                        (SecurityProtocolType)3072 /* Tls12 */ |
                        (SecurityProtocolType)768  /* Tls11 */ |
                        SecurityProtocolType.Tls;
                }
                catch { /* enum value unsupported on this runtime — ignore */ }

                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("user-agent", "MSCLocAPI");
                    string text = wc.DownloadString(VersionUrl);
                    if (!string.IsNullOrEmpty(text)) latest = text.Trim();
                }
            }
            catch { latest = null; }
        }

        /// <summary>True once a strictly newer version has been confirmed.</summary>
        public static bool UpdateAvailable
        {
            get
            {
                string l = latest;
                return !string.IsNullOrEmpty(l) && IsNewer(l, current);
            }
        }

        public static string LatestVersion { get { return latest; } }

        // Dotted semantic compare: returns true when a > b (major.minor.patch).
        private static bool IsNewer(string a, string b)
        {
            try
            {
                int[] pa = Parse(a), pb = Parse(b);
                for (int i = 0; i < 3; i++)
                {
                    if (pa[i] > pb[i]) return true;
                    if (pa[i] < pb[i]) return false;
                }
                return false;
            }
            catch { return false; }
        }

        private static int[] Parse(string v)
        {
            string[] parts = v.Split('.');
            int[] r = new int[3];
            for (int i = 0; i < 3 && i < parts.Length; i++)
            {
                int n;
                int.TryParse(parts[i], out n);
                r[i] = n;
            }
            return r;
        }
    }
}
