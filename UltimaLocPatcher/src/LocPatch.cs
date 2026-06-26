using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;

namespace UltimaLoc
{
    /// <summary>
    /// Applies a Harmony transpiler to every method of each target assembly,
    /// rewriting `ldstr` operands whose MakeId is present in LocStore.Map to
    /// their translation. The original .dll on disk is never touched.
    /// Uses the Harmony 1.x API bundled with MSCLoader (0Harmony 1.2).
    /// </summary>
    public static class LocPatch
    {
        private const BindingFlags MemberFlags =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Patch all loaded assemblies whose simple name is in LocStore.Targets.
        /// Returns the number of methods patched.
        /// </summary>
        public static int ApplyToLoadedTargets(HarmonyInstance harmony)
        {
            HarmonyMethod transpiler = new HarmonyMethod(
                typeof(LocPatch).GetMethod(nameof(Transpiler), BindingFlags.Static | BindingFlags.NonPublic));

            int patched = 0;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name;
                try { name = asm.GetName().Name; }
                catch { continue; }

                if (!LocStore.Targets.Contains(name)) continue;
                patched += PatchAssembly(harmony, asm, transpiler);
            }
            return patched;
        }

        private static int PatchAssembly(HarmonyInstance harmony, Assembly asm, HarmonyMethod transpiler)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            catch { return 0; }

            int count = 0;
            foreach (Type type in types)
            {
                if (type == null) continue;

                List<MethodBase> members = new List<MethodBase>();
                try { members.AddRange(type.GetMethods(MemberFlags)); } catch { }
                try { members.AddRange(type.GetConstructors(MemberFlags)); } catch { }

                foreach (MethodBase m in members)
                {
                    if (m.IsAbstract || m.ContainsGenericParameters) continue;
                    try { if (m.GetMethodBody() == null) continue; }
                    catch { continue; }

                    try
                    {
                        harmony.Patch(m, null, null, transpiler);
                        count++;
                    }
                    catch
                    {
                        // Some methods can't be patched (intrinsics, etc.) — skip.
                    }
                }
            }
            return count;
        }

        // Harmony transpiler: swap translatable ldstr operands in-place.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction ins in instructions)
            {
                if (ins.opcode == OpCodes.Ldstr && ins.operand is string original)
                {
                    string translated;
                    if (LocStore.TryTranslate(original, out translated))
                    {
                        ins.operand = translated;
                    }
                }
                yield return ins;
            }
        }
    }
}
