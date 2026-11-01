using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xaml;
using System.Windows.Markup;

class Program
{
    static string asmDir;
    static void Main(string[] args)
    {
        string dir = args[0], outDir = args[1];
        asmDir = args[2];
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            var n = new AssemblyName(e.Name).Name;
            foreach (var ext in new[] { ".exe", ".dll" })
            {
                var p = Path.Combine(asmDir, n + ext);
                if (File.Exists(p)) return Assembly.LoadFrom(p);
            }
            return null;
        };
        foreach (var f in Directory.GetFiles(dir, "*.baml", SearchOption.AllDirectories))
        {
            string rel = f.Substring(dir.Length).TrimStart('\\');
            string outp = Path.Combine(outDir, Path.ChangeExtension(rel, ".xaml"));
            Directory.CreateDirectory(Path.GetDirectoryName(outp));
            try
            {
                using (var fs = File.OpenRead(f))
                using (var xw = XmlWriter.Create(outp, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }))
                {
                    var reader = new System.Windows.Baml2006.Baml2006Reader(fs);
                    var writer = new XamlXmlWriter(xw, reader.SchemaContext);
                    while (reader.Read()) writer.WriteNode(reader);
                    writer.Close();
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText(outp, "<!-- ERROR: " + ex.Message + " -->");
                Console.WriteLine("ERR " + rel + ": " + ex.Message);
            }
        }
        Console.WriteLine("done");
    }
}
