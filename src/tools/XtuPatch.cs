using System;
using System.Collections.Generic;
using System.IO;
using System.Resources;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

// XtuPatch: rebuild .resources with substituted BAML, and patch .NET assemblies
// (embedded resources, string literals, neutralize methods) using Mono.Cecil.
class XtuPatch
{
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s2, e2) => { Console.Error.WriteLine("UNHANDLED: " + e2.ExceptionObject.GetType().FullName + " :: " + ((e2.ExceptionObject as Exception) == null ? "" : ((Exception)e2.ExceptionObject).Message)); };
        try { Run(args); } catch (Exception ex) { Console.Error.WriteLine("ERROR " + ex.GetType().FullName + " :: " + ex.Message); var ie = ex.InnerException; while (ie != null) { Console.Error.WriteLine("  INNER " + ie.GetType().FullName + " :: " + ie.Message); ie = ie.InnerException; } Console.Error.WriteLine(ex.StackTrace); Environment.Exit(2); }
    }

    static void Run(string[] args)
    {
        switch (args[0])
        {
            case "resbuild": ResBuild(args[1], args[2], args[3]); break;
            case "patchasm": PatchAsm(args[1], args[2], Opt(args, 3), Opt(args, 4), Opt(args, 5)); break;
            case "liststr": ListStrings(args[1], args[2]); break;
            default: Console.WriteLine("unknown mode"); break;
        }
    }

    static string Opt(string[] a, int i) { return a.Length > i && a[i].Length > 0 && a[i] != "-" ? a[i] : null; }

    // resbuild <origResources> <replacementDir> <outResources>
    static void ResBuild(string orig, string replDir, string outp)
    {
        var entries = new List<KeyValuePair<string, byte[]>>();
        using (var fs = File.OpenRead(orig))
        using (var rr = new ResourceReader(fs))
        {
            foreach (System.Collections.DictionaryEntry de in rr)
            {
                string name = (string)de.Key;
                string type; byte[] data;
                rr.GetResourceData(name, out type, out data);
                if (type == "ResourceTypeCode.Stream" && data.Length >= 4)
                {
                    byte[] raw = new byte[data.Length - 4];
                    Array.Copy(data, 4, raw, 0, raw.Length);
                    data = raw;
                }
                if (replDir != null)
                {
                    string rel = name.Replace('/', Path.DirectorySeparatorChar);
                    string cand = Path.Combine(replDir, rel);
                    if (File.Exists(cand)) data = File.ReadAllBytes(cand);
                }
                entries.Add(new KeyValuePair<string, byte[]>(name, data));
            }
        }
        using (var fs = File.Create(outp))
        using (var rw = new ResourceWriter(fs))
        {
            foreach (var kv in entries) rw.AddResource(kv.Key, new MemoryStream(kv.Value));
            rw.Generate();
        }
        Console.WriteLine("resbuild entries=" + entries.Count + " -> " + outp);
    }

    // patchasm <srcAsm> <dstAsm> [resMapTsv] [strMapTsv] [neutralMapTsv]
    // resMap:  "resourceName<TAB>filePath"
    // strMap:  "old<TAB>new"   (exact ldstr match)
    // neutMap: "TypeFullName<TAB>MethodName<TAB>intValue"
    static void PatchAsm(string src, string dst, string resMap, string strMap, string neutMap)
    {
        var resRepl = new Dictionary<string, string>();
        if (resMap != null && File.Exists(resMap))
            foreach (var line in File.ReadAllLines(resMap))
            {
                if (line.Length == 0) continue;
                var p = line.Split('\t');
                if (p.Length < 2) continue;
                resRepl[p[0]] = p[1];
            }

        var strRepl = new Dictionary<string, string>(StringComparer.Ordinal);
        if (strMap != null && File.Exists(strMap))
            foreach (var line in File.ReadAllLines(strMap))
            {
                if (line.Length == 0) continue;
                int i = line.IndexOf('\t');
                if (i < 0) continue;
                strRepl[Unesc(line.Substring(0, i))] = Unesc(line.Substring(i + 1));
            }

        var neut = new List<Tuple<string, string, int>>();
        if (neutMap != null && File.Exists(neutMap))
            foreach (var line in File.ReadAllLines(neutMap))
            {
                if (line.Length == 0) continue;
                var p = line.Split('\t');
                if (p.Length < 3) continue;
                neut.Add(Tuple.Create(p[0], p[1], int.Parse(p[2])));
            }

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(src)));
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(dst)));
        var rp = new ReaderParameters { ReadingMode = ReadingMode.Immediate, InMemory = true, AssemblyResolver = resolver };
        var asm = AssemblyDefinition.ReadAssembly(src, rp);
        var module = asm.MainModule;

        int resReplaced = 0;
        if (resRepl.Count > 0)
        {
            var toReplace = new List<Resource>();
            foreach (var res in module.Resources)
            {
                string path;
                if (resRepl.TryGetValue(res.Name, out path) && File.Exists(path)) toReplace.Add(res);
            }
            foreach (var res in toReplace)
            {
                var er = (EmbeddedResource)res;
                byte[] data = File.ReadAllBytes(resRepl[res.Name]);
                module.Resources.Remove(res);
                module.Resources.Add(new EmbeddedResource(er.Name, er.Attributes, data));
                resReplaced++;
            }
        }

        int strReplaced = 0;
        if (strRepl.Count > 0)
        {
            foreach (var type in AllTypes(module))
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;
                    foreach (var ins in method.Body.Instructions)
                        if (ins.OpCode == OpCodes.Ldstr)
                        {
                            string old = (string)ins.Operand, neu;
                            if (old != null && strRepl.TryGetValue(old, out neu))
                            {
                                ins.Operand = neu;
                                strReplaced++;
                            }
                        }
                }
        }

        int neutCount = 0;
        if (neut.Count > 0)
        {
            foreach (var type in AllTypes(module))
                foreach (var method in type.Methods)
                {
                    foreach (var spec in neut)
                    {
                        if (type.FullName != spec.Item1 || method.Name != spec.Item2) continue;
                        if (!method.HasBody) continue;
                        var body = method.Body;
                        body.Instructions.Clear();
                        body.ExceptionHandlers.Clear();
                        body.Variables.Clear();
                        body.InitLocals = false;
                        body.MaxStackSize = 8;
                        var il = body.GetILProcessor();
                        il.Append(il.Create(OpCodes.Ldc_I4, spec.Item3));
                        il.Append(il.Create(OpCodes.Ret));
                        neutCount++;
                    }
                }
        }

        var wp = new WriterParameters();
        asm.Write(dst, wp);
        asm.Dispose();
        Console.WriteLine("patchasm res=" + resReplaced + " strs=" + strReplaced + " neut=" + neutCount + " -> " + dst);
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

    // liststr <asm> <outTsv>
    static void ListStrings(string src, string outp)
    {
        var asm = AssemblyDefinition.ReadAssembly(src, new ReaderParameters { ReadingMode = ReadingMode.Immediate, InMemory = true });
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var type in AllTypes(asm.MainModule))
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;
                foreach (var ins in method.Body.Instructions)
                    if (ins.OpCode == OpCodes.Ldstr)
                    {
                        string s = (string)ins.Operand;
                        if (s == null) continue;
                        int c;
                        counts.TryGetValue(s, out c);
                        counts[s] = c + 1;
                    }
            }
        var sb = new StringBuilder();
        sb.AppendLine("count\tvalue");
        foreach (var kv in counts) sb.AppendLine(kv.Value + "\t" + Esc(kv.Key));
        File.WriteAllText(outp, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine("strings=" + counts.Count + " -> " + outp);
    }

    static string Esc(string s) { return s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n"); }
    static string Unesc(string s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char c = s[++i];
                if (c == 't') sb.Append('\t');
                else if (c == 'r') sb.Append('\r');
                else if (c == 'n') sb.Append('\n');
                else if (c == '\\') sb.Append('\\');
                else { sb.Append('\\'); sb.Append(c); }
            }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }
}

