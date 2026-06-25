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
//        → prints JSON array [{ "id": "...", "text": "...", "context"?: {...} }]
//          to stdout. One entry per distinct non-empty `ldstr` literal, in
//          first-seen order. `context` (optional) carries IL-usage signals
//          aggregated across every occurrence of the literal:
//              { "sinks":  ["Namespace.Type::Method", ...],   // APIs it flows into
//                "fields": ["fieldName", ...] }               // fields it's stored in
//          The app's string classifier uses these to tell player-facing text
//          from technical identifiers/keys. Omitted when no signals were found.
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

        // Aggregate per id (literals dedupe by id), preserving first-seen order
        // so the extract output stays stable across runs.
        var order = new List<string>();
        var byId = new Dictionary<string, Entry>();

        foreach (var type in module.GetTypes())
        {
            foreach (var method in type.Methods)
            {
                if (method.Body == null) continue;
                var instrs = method.Body.Instructions;
                for (int i = 0; i < instrs.Count; i++)
                {
                    var instr = instrs[i];
                    if (instr.OpCode.Code != Code.Ldstr) continue;
                    if (instr.Operand is not string s) continue;
                    if (string.IsNullOrWhiteSpace(s)) continue;

                    string id = MakeId(s);
                    if (!byId.TryGetValue(id, out var entry))
                    {
                        entry = new Entry { Id = id, Text = s };
                        byId[id] = entry;
                        order.Add(id);
                    }
                    AnalyzeUsage(instrs, i, entry);
                }
            }
        }

        var entries = new List<object>(order.Count);
        foreach (var id in order)
        {
            var e = byId[id];
            if (e.Sinks.Count == 0 && e.Fields.Count == 0)
            {
                entries.Add(new { id = e.Id, text = e.Text });
                continue;
            }

            var context = new Dictionary<string, object>();
            if (e.Sinks.Count > 0) context["sinks"] = e.Sinks.ToArray();
            if (e.Fields.Count > 0) context["fields"] = e.Fields.ToArray();
            entries.Add(new { id = e.Id, text = e.Text, context });
        }

        Console.OutputEncoding = Encoding.UTF8;
        Console.Out.Write(JsonSerializer.Serialize(entries));
        return 0;
    }

    // Inspect what consumes the literal at `ldstrIndex`: the first method call
    // (sink) or field store within a short forward window. A deliberate
    // heuristic — it nails the common patterns (`ldstr; call Find`,
    // `ldarg.0; ldstr; stfld name`) and bails on control flow so it never
    // misattributes across branches. The app side tolerates occasional noise.
    static void AnalyzeUsage(IList<Instruction> instrs, int ldstrIndex, Entry entry)
    {
        int end = Math.Min(ldstrIndex + 7, instrs.Count);
        for (int j = ldstrIndex + 1; j < end; j++)
        {
            var instr = instrs[j];
            var code = instr.OpCode.Code;

            if (code == Code.Ldstr) break; // next literal begins

            if (code == Code.Call || code == Code.Callvirt || code == Code.Newobj)
            {
                if (instr.Operand is IMethod m && m.DeclaringType != null)
                {
                    string name = code == Code.Newobj
                        ? $"{m.DeclaringType.FullName}::.ctor"
                        : $"{m.DeclaringType.FullName}::{m.Name}";
                    entry.Sinks.Add(name);
                }
                break;
            }

            if (code == Code.Stfld || code == Code.Stsfld)
            {
                if (instr.Operand is IField f)
                {
                    string? fieldName = f.Name?.String;
                    if (!string.IsNullOrEmpty(fieldName))
                        entry.Fields.Add(fieldName);
                }
                break;
            }

            var flow = instr.OpCode.FlowControl;
            if (flow == FlowControl.Branch || flow == FlowControl.Cond_Branch ||
                flow == FlowControl.Return || flow == FlowControl.Throw)
                break;
        }
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

// Accumulates a literal plus the distinct IL-usage signals seen across all of
// its occurrences (see AnalyzeUsage / the extract context contract).
sealed class Entry
{
    public string Id = "";
    public string Text = "";
    public HashSet<string> Sinks { get; } = new();
    public HashSet<string> Fields { get; } = new();
}
