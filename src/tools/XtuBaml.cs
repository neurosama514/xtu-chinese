using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Runtime.Serialization;

// Tool that manipulates WPF BAML via internal MS.Internal.Globalization tree APIs.
class XtuBaml
{
    static string asmDir;
    static Assembly pf;
    static Type tDeser, tSer, tTree, tLocalizer, tNode;
    static MethodInfo mLoad, mSerialize;
    static PropertyInfo pSize, pItem;
    static Dictionary<string, PropertyInfo> propCache = new Dictionary<string, PropertyInfo>();
    static BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static void Init()
    {
        tDeser = pf.GetType("MS.Internal.Globalization.BamlResourceDeserializer", true);
        tSer = pf.GetType("MS.Internal.Globalization.BamlResourceSerializer", true);
        tTree = pf.GetType("MS.Internal.Globalization.BamlTree", true);
        tLocalizer = pf.GetType("System.Windows.Markup.Localizer.BamlLocalizer", true);
        tNode = pf.GetType("MS.Internal.Globalization.BamlTreeNode", true);
        mLoad = tDeser.GetMethod("LoadBaml", Flags);
        mSerialize = tSer.GetMethod("Serialize", Flags);
        pSize = tTree.GetProperty("Size", Flags);
        pItem = tTree.GetProperty("Item", Flags);
    }

    static PropertyInfo Prop(object o, string name)
    {
        string key = o.GetType().FullName + "|" + name;
        PropertyInfo pi;
        if (!propCache.TryGetValue(key, out pi))
        {
            pi = o.GetType().GetProperty(name, Flags);
            if (pi == null) throw new Exception("missing property " + name + " on " + o.GetType().FullName);
            propCache[key] = pi;
        }
        return pi;
    }

    static object Get(object o, string name) { return Prop(o, name).GetValue(o, null); }
    static void Set(object o, string name, object v) { Prop(o, name).SetValue(o, v, null); }

    static object LoadTree(Stream s) { return mLoad.Invoke(null, new object[] { s }); }
    static int Size(object tree) { return (int)pSize.GetValue(tree, null); }
    static object At(object tree, int i) { return pItem.GetValue(tree, new object[] { i }); }

    static void Setup()
    {
        pf = typeof(System.Windows.Markup.Localizer.BamlLocalizer).Assembly;
        asmDir = Environment.GetEnvironmentVariable("XTU_ASM_DIR");
        if (!string.IsNullOrEmpty(asmDir))
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s2, e2) =>
            {
                var n = new AssemblyName(e2.Name).Name;
                foreach (var ext in new[] { ".exe", ".dll" })
                {
                    var f2 = Path.Combine(asmDir, n + ext);
                    if (File.Exists(f2)) return Assembly.LoadFrom(f2);
                }
                return null;
            };
        }
        Init();
    }

    static void Main(string[] args)
    {
        Setup();
        string mode = args[0];
        if (mode == "roundtrip")
        {
            string inDir = args[1], outDir = args[2];
            int ok = 0, fail = 0;
            foreach (var f in Directory.GetFiles(inDir, "*.baml", SearchOption.AllDirectories))
            {
                string rel = f.Substring(inDir.Length).TrimStart('\\');
                string outp = Path.Combine(outDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(outp));
                try
                {
                    using (var fs = File.OpenRead(f))
                    {
                        var tree = LoadTree(fs);
                        using (var os = File.Create(outp)) Serialize(tree, os);
                    }
                    ok++;
                }
                catch (Exception ex) { fail++; Console.WriteLine("FAIL " + rel + ": " + Inner(ex)); }
            }
            Console.WriteLine("roundtrip ok=" + ok + " fail=" + fail);
        }
        else if (mode == "dump")
        {
            string inDir = args[1], outFile = args[2];
            var sb = new StringBuilder();
            sb.AppendLine("file\tindex\tkind\townerType\tpropName\tvalue");
            int total = 0;
            foreach (var f in Directory.GetFiles(inDir, "*.baml", SearchOption.AllDirectories))
            {
                string rel = f.Substring(inDir.Length).TrimStart('\\');
                try
                {
                    using (var fs = File.OpenRead(f))
                    {
                        var tree = LoadTree(fs);
                        for (int i = 0; i < Size(tree); i++)
                        {
                            var n = At(tree, i);
                            string tn = n.GetType().Name;
                            string kind = null, owner = "", prop = "", val = null;
                            if (tn == "BamlPropertyNode")
                            {
                                kind = "prop";
                                owner = (string)Get(n, "OwnerTypeFullName");
                                prop = (string)Get(n, "PropertyName");
                                val = (string)Get(n, "Value");
                            }
                            else if (tn == "BamlTextNode")
                            {
                                kind = "text";
                                val = (string)Get(n, "Content");
                                object par = Get(n, "Parent");
                                if (par != null) owner = par.GetType().Name + ":" + (string)Get(par, "Content");
                            }
                            else if (tn == "BamlLiteralContentNode")
                            {
                                kind = "literal";
                                val = (string)Get(n, "Content");
                                object par = Get(n, "Parent");
                                if (par != null) owner = par.GetType().Name;
                            }
                            else continue;
                            if (val == null) continue;
                            sb.Append(rel).Append('\t').Append(i).Append('\t').Append(kind).Append('\t')
                              .Append(Esc(owner)).Append('\t').Append(Esc(prop)).Append('\t').Append(Esc(val)).Append('\n');
                            total++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.Append(rel).Append("\t-1\tERROR\t\t\t").Append(Esc(Inner(ex))).Append('\n');
                }
            }
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(true));
            Console.WriteLine("dump entries=" + total);
        }
        else if (mode == "validate")
        {
            string inDir = args[1];
            int ok = 0, fail = 0;
            foreach (var f in Directory.GetFiles(inDir, "*.baml", SearchOption.AllDirectories))
            {
                string rel = f.Substring(inDir.Length).TrimStart('\\');
                try
                {
                    using (var fs = File.OpenRead(f))
                    {
                        var r2 = new System.Windows.Baml2006.Baml2006Reader(fs);
                        int nodes = 0;
                        while (r2.Read()) nodes++;
                        if (nodes == 0) throw new Exception("zero nodes");
                    }
                    ok++;
                }
                catch (Exception ex) { fail++; Console.WriteLine("INVALID " + rel + ": " + Inner(ex)); }
            }
            Console.WriteLine("validate ok=" + ok + " fail=" + fail);
        }
        else if (mode == "apply")
        {
            string inDir = args[1], mapFile = args[2], outDir = args[3];
            var map = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(mapFile))
            {
                if (line.StartsWith("file\t") || line.Length == 0) continue;
                var parts = line.Split('\t');
                if (parts.Length < 6) continue;
                map[parts[0] + "|" + parts[1]] = Unesc(parts[5]);
            }
            int changed = 0, files = 0, errs = 0;
            foreach (var f in Directory.GetFiles(inDir, "*.baml", SearchOption.AllDirectories))
            {
                string rel = f.Substring(inDir.Length).TrimStart('\\');
                string outp = Path.Combine(outDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(outp));
                try
                {
                    using (var fs = File.OpenRead(f))
                    {
                        var tree = LoadTree(fs);
                        for (int i = 0; i < Size(tree); i++)
                        {
                            string newVal;
                            if (!map.TryGetValue(rel + "|" + i, out newVal)) continue;
                            var n = At(tree, i);
                            string tn = n.GetType().Name;
                            if (tn == "BamlPropertyNode") { Set(n, "Value", newVal); changed++; }
                            else if (tn == "BamlLiteralContentNode") { Set(n, "Content", newVal); changed++; }
                            else if (tn == "BamlTextNode") { ReplaceTextNode(n, newVal); changed++; }
                        }
                        using (var os = File.Create(outp)) Serialize(tree, os);
                        files++;
                    }
                }
                catch (Exception ex) { errs++; Console.WriteLine("FAIL " + rel + ": " + Inner(ex)); }
            }
            Console.WriteLine("apply files=" + files + " changed=" + changed + " errors=" + errs);
        }
    }

    static void ReplaceTextNode(object node, string newVal)
    {
        var ctor = node.GetType().GetConstructor(Flags, null, new Type[] { typeof(string), typeof(string), typeof(string) }, null);
        object repl = ctor.Invoke(new object[] { newVal, null, null });
        object parent = Get(node, "Parent");
        var children = (IList)Get(parent, "Children");
        int idx = children.IndexOf(node);
        if (idx < 0) throw new Exception("text node parent mismatch");
        children[idx] = repl;
        Set(repl, "Parent", parent);
    }

    static void Serialize(object tree, Stream outStream)
    {
        object loc = FormatterServices.GetUninitializedObject(tLocalizer);
        mSerialize.Invoke(null, new object[] { loc, tree, outStream });
    }

    static string Inner(Exception ex)
    {
        while (ex.InnerException != null) ex = ex.InnerException;
        return ex.GetType().Name + ": " + ex.Message;
    }

    static string Esc(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
    }

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

