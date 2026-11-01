using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

// IlTool: modes
//   callers <asm> <methodName>   -> methods that call a method with this name (prints IL around call)
//   type <asm> <typeSubstring>   -> dump all methods of matching types
class IlTool
{
    static void Main(string[] args)
    {
        string mode = args[0], path = args[1], needle = args[2];
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadingMode = ReadingMode.Immediate, AssemblyResolver = resolver });
        if (mode == "callers")
        {
            foreach (var type in AllTypes(asm.MainModule))
                foreach (var m in type.Methods)
                {
                    if (!m.HasBody) continue;
                    foreach (var ins in m.Body.Instructions)
                    {
                        var mr = ins.Operand as MethodReference;
                        if (mr != null && mr.Name == needle)
                        {
                            Console.WriteLine("CALLER: " + type.FullName + "." + m.Name + "  at " + ins.Offset.ToString("X4") + " -> " + mr.DeclaringType.Name + "." + mr.Name);
                            break;
                        }
                    }
                }
        }
        else if (mode == "type")
        {
            foreach (var type in AllTypes(asm.MainModule))
            {
                if (!type.FullName.Contains(needle)) continue;
                Console.WriteLine("=== TYPE " + type.FullName);
                foreach (var f in type.Fields) Console.WriteLine("  FIELD " + f.Name);
                foreach (var m in type.Methods)
                {
                    Console.WriteLine("  --- " + m.Name + "(" + JoinParams(m) + ")");
                    if (!m.HasBody) continue;
                    foreach (var ins in m.Body.Instructions)
                        Console.WriteLine("     " + ins.Offset.ToString("X4") + " " + ins.OpCode.Name + " " + Fmt(ins.Operand));
                }
            }
        }
    }

    static string JoinParams(MethodDefinition m)
    {
        var parts = new List<string>();
        foreach (var p in m.Parameters) parts.Add(p.ParameterType.Name + " " + p.Name);
        return string.Join(", ", parts.ToArray());
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
