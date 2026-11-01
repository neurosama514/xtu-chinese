using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

// ildump <assembly> <needle> : print IL of methods whose body references needle string
class IlDump
{
    static void Main(string[] args)
    {
        string path = args[0];
        string needle = args.Length > 1 ? args[1] : null;
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadingMode = ReadingMode.Immediate, AssemblyResolver = resolver });
        foreach (var type in AllTypes(asm.MainModule))
        {
            foreach (var m in type.Methods)
            {
                if (!m.HasBody) continue;
                bool hit = false;
                foreach (var ins in m.Body.Instructions)
                {
                    if (ins.OpCode == OpCodes.Ldstr && needle != null && ((string)ins.Operand ?? "").Contains(needle)) { hit = true; break; }
                    if (needle == null && (ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt))
                    {
                        var mr = ins.Operand as MethodReference;
                        if (mr != null && mr.Name.Contains("Verify")) { hit = true; break; }
                    }
                }
                if (!hit) continue;
                Console.WriteLine("=== " + type.FullName + "." + m.Name);
                foreach (var ins in m.Body.Instructions)
                    Console.WriteLine("  " + ins.Offset.ToString("X4") + " " + ins.OpCode.Name + " " + Fmt(ins.Operand));
            }
        }
    }

    static string Fmt(object o)
    {
        var s = o as string;
        if (s != null) return "\"" + s.Replace("\n", "\\n") + "\"";
        var mr = o as MethodReference;
        if (mr != null) return mr.DeclaringType.Name + "." + mr.Name;
        var fr = o as FieldReference;
        if (fr != null) return fr.DeclaringType.Name + "::" + fr.Name;
        return o == null ? "" : o.ToString();
    }

    static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
    {
        var stack = new Stack<TypeDefinition>();
        foreach (var t in module.Types) stack.Push(t);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            yield return t;
            foreach (var n in t.NestedTypes) stack.Push(n);
        }
    }
}
