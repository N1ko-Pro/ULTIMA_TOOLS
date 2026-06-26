using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UltimaLoc;

// ─────────────────────────────────────────────────────────────────────────────
//  UltimaLocPatcher unit tests (pure core).
//    • Property 3 — MakeId determinism + the cross-language id contract,
//      cross-checked against golden vectors produced by Node's makeStringId.
//    • Property 5 — the patcher translates a literal iff its id is present with
//      a non-empty value; misses leave the original.
// ─────────────────────────────────────────────────────────────────────────────

int pass = 0, fail = 0;

void Test(string name, Action body)
{
    try { body(); pass++; Console.WriteLine("  [PASS] " + name); }
    catch (Exception e) { fail++; Console.WriteLine("  [FAIL] " + name + " :: " + e.Message); }
}

void Assert(bool cond, string msg)
{
    if (!cond) throw new Exception(msg);
}

static string ReferenceId(string text)
{
    using var sha = SHA256.Create();
    var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
    var sb = new StringBuilder("u");
    for (int i = 0; i < 8; i++) sb.Append(hash[i].ToString("x2"));
    return sb.ToString();
}

Console.WriteLine("LocId — contract & determinism (Property 3)");

// Golden vectors copied from Node's makeStringId (stringId.js) — these MUST
// match byte-for-byte across the toolchain.
var golden = new Dictionary<string, string>
{
    [""]                        = "ue3b0c44298fc1c14",
    ["Hello"]                   = "u185f8db32271fe25",
    ["Привет, мир"]             = "u2a2e76364df5ab8f",
    ["MSCQualityTweaks_ITEMS"]  = "ub077c140ffe23b85",
    ["car jack(itemx)"]         = "ua64f26589d4a9d02",
};

Test("matches Node golden vectors", () =>
{
    foreach (var kv in golden)
        Assert(LocId.Make(kv.Key) == kv.Value, $"'{kv.Key}' → {LocId.Make(kv.Key)} ≠ {kv.Value}");
});

Test("matches the documented formula for arbitrary input", () =>
{
    foreach (var s in new[] { "a", "ABC abc 123", "съешь ещё", "tab\there", "emoji🎮" })
        Assert(LocId.Make(s) == ReferenceId(s), $"mismatch for '{s}'");
});

Test("is deterministic and well-formed (u + 16 hex)", () =>
{
    var rng = new Random(1234);
    for (int i = 0; i < 5000; i++)
    {
        string s = "s" + rng.Next() + "-" + i;
        string id = LocId.Make(s);
        Assert(id == LocId.Make(s), "non-deterministic for " + s);
        Assert(id.Length == 17 && id[0] == 'u', "bad shape: " + id);
        for (int k = 1; k < id.Length; k++)
            Assert(Uri.IsHexDigit(id[k]) && !char.IsUpper(id[k]), "non-lower-hex: " + id);
    }
});

Console.WriteLine("\nLocStore.TryTranslate — decision (Property 5)");

Test("translates iff id present with a non-empty value", () =>
{
    LocStore.Reset();
    LocStore.Map[LocId.Make("Hello")] = "Привет";
    LocStore.Map[LocId.Make("Bye")] = ""; // empty → must be ignored

    Assert(LocStore.TryTranslate("Hello", out var tr) && tr == "Привет", "Hello not translated");
    Assert(!LocStore.TryTranslate("Bye", out _), "empty value must not translate");
    Assert(!LocStore.TryTranslate("Unknown", out _), "absent id must not translate");
    Assert(!LocStore.TryTranslate("", out _), "empty input must not translate");
    Assert(!LocStore.TryTranslate(null, out _), "null input must not translate");
});

Test("a miss leaves the original (out is null)", () =>
{
    LocStore.Reset();
    LocStore.Map[LocId.Make("X")] = "Икс";
    Assert(!LocStore.TryTranslate("Y", out var tr), "Y should miss");
    Assert(tr == null, "miss must yield null translation");
});

Console.WriteLine($"\n{pass} passed, {fail} failed");
Environment.Exit(fail == 0 ? 0 : 1);
