using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

// ─────────────────────────────────────────────────────────────────────────────
//  MscLocTool — string extraction / injection for .NET mod assemblies (dnlib).
//
//  Usage:
//    MscLocTool extract <input.dll>
//        → prints JSON array [{ "id": "...", "text": "..." }] to stdout.
//          One entry per distinct non-empty `ldstr` literal, in first-seen order.
//
//    MscLocTool inject <input.dll> <translations.json> <output.dll>
//        → translations.json is an object { "<id>": "<translated>" }.
//          Every `ldstr` whose literal maps to a non-empty translation is
//          rewritten; the modified assembly is saved to <output.dll>.
//          Prints JSON { "replaced": <count> } to stdout.
//
//  `id` is a stable hash of the original string (sha256, first 16 hex chars,
//  prefixed with 'u'). The Node side computes ids the same way, so ids line up
//  across extract/inject and across app sessions. MUST stay in sync with
//  Backend/games/mysummercar/dll_utils/stringId.js.
// ─────────────────────────────────────────────────────────────────────────────

static class Program
{
    static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 2 && args[0] == "extract")
                return Extract(args[1]);

            if (args.Length >= 4 && args[0] == "inject")
                return Inject(args[1], args[2], args[3]);

            Console.Error.WriteLine("Usage: MscLocTool extract <input.dll> | inject <input.dll> <translations.json> <output.dll>");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static string MakeId(string text)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var sb = new StringBuilder("u");
        for (int i = 0; i < 8; i++) sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }

    static int Extract(string inputPath)
    {
        using var module = ModuleDefMD.Load(inputPath);

        var seen = new HashSet<string>();
        var entries = new List<object>();

        foreach (var type in module.GetTypes())
        {
            foreach (var method in type.Methods)
            {
                if (method.Body == null) continue;
                foreach (var instr in method.Body.Instructions)
                {
                    if (instr.OpCode.Code != Code.Ldstr) continue;
                    if (instr.Operand is not string s) continue;
                    if (string.IsNullOrWhiteSpace(s)) continue;

                    string id = MakeId(s);
                    if (seen.Add(id))
                        entries.Add(new { id, text = s });
                }
            }
        }

        Console.OutputEncoding = Encoding.UTF8;
        Console.Out.Write(JsonSerializer.Serialize(entries));
        return 0;
    }

    static int Inject(string inputPath, string translationsPath, string outputPath)
    {
        var json = File.ReadAllText(translationsPath);
        var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                           ?? new Dictionary<string, string>();

        using var module = ModuleDefMD.Load(inputPath);
        int replaced = 0;

        foreach (var type in module.GetTypes())
        {
            foreach (var method in type.Methods)
            {
                if (method.Body == null) continue;
                foreach (var instr in method.Body.Instructions)
                {
                    if (instr.OpCode.Code != Code.Ldstr) continue;
                    if (instr.Operand is not string s) continue;

                    string id = MakeId(s);
                    if (translations.TryGetValue(id, out var translated) && !string.IsNullOrEmpty(translated))
                    {
                        instr.Operand = translated;
                        replaced++;
                    }
                }
            }
        }

        module.Write(outputPath);

        Console.OutputEncoding = Encoding.UTF8;
        Console.Out.Write(JsonSerializer.Serialize(new { replaced }));
        return 0;
    }
}
