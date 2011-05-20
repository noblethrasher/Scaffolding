using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System.IO;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using CodeGeneration;

namespace Sql_Object_Generator
{
    class Program
    {

        static string path;
        
        static Dictionary<string, Action<string[]>> act = new Dictionary<string,Action<string[]>>()
        {
            {"create-objects", CreateInterface}
        };


        static void Main(string[] args)
        {
            path = Environment.CurrentDirectory;
            
            if (args.Length > 0)
            {
                act[args[0]] (args.Skip (1).ToArray ());
            }
        }

        static int IndexOf(string[] arr, string elem)
        {
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].Equals (elem, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        static void CreateInterface(string[] args)
        {
            string conn_str = null, datasource = null;
            var c = IndexOf (args, "-c");

            if (c != -1)
            {
                if (c + 1 < args.Length)
                {
                    conn_str = args[c + 1];

                    datasource = args[c + 1].Split (';').Where (x => x.Trim().StartsWith ("initial", StringComparison.OrdinalIgnoreCase)).ElementAt (0).Split ('=')[1];
                }
            }
            else
            {
                var ds = IndexOf (args, "-ds");
                var ct = IndexOf (args, "-ct");
                var un = IndexOf (args, "-un");
                var pw = IndexOf (args, "-pw");
                var iy = IndexOf (args, "-is");

                var has_data_source_and_catalog = new[] {ds, ct}.All(x => x > -1 && args.Length <= x + 1);
                var has_un_and_pw = new[] {un, pw}.All(x => x > -1 && args.Length <= x + 1);
                var has_integrated_sec = iy > -1 && args.Length <= iy + 1;

                if (has_data_source_and_catalog)
                {
                    conn_str += "data source = " + args[ds + 1] + "; " + "initial catalog = " + args[ct + 1];

                    datasource = args[ct + 1];
                }

                if (has_un_and_pw)
                {
                    conn_str += ";user name = " + args[un + 1] + ";" + "password = " + args[pw + 1];
                }
                else if (has_integrated_sec)
                {
                    conn_str += ";integrated security = sspi";
                }

                

            }
            
            
            var conn = new SqlConnection (conn_str);
            var server = new Server (new ServerConnection (conn));

            var database = server.Databases[datasource.Trim()];
            var tables = database.Tables.ToEnumerable ();

            var sb = new StringBuilder ();
            sb.AppendLine ("using System;");
            sb.AppendLine ("using Scaffolding;");
            sb.AppendLine ("namespace " + datasource.Trim ());
            sb.AppendLine ("{");

            var list = new List<Table> ();

            foreach (var table in tables)
            {
                list.Add (table);
            }

            

            foreach (var table in list)
            {
                var tblIface = new TableInterface (table);

                sb.AppendLine (tblIface.ToString (1), 1);
                sb.AppendLine ();
            }

            sb.AppendLine ("}");

            var writer = new StreamWriter (Path.Combine (path, "foo.cs"));

            writer.Write (sb.ToString ());

            writer.Close ();

            CreateHelperMethods ();
        }

        static void CreateHelperMethods()
        {
            var compiler = new CSharpCodeProvider ();

            var options = new CompilerParameters ();

            options.GenerateExecutable = false;
            options.GenerateInMemory = true;

            options.ReferencedAssemblies.Add (@"C:\Users\Rod\Documents\MyLib\Scaffolding.dll");
            options.ReferencedAssemblies.Add ("System.Data.dll");
            options.ReferencedAssemblies.Add ("System.dll");

            var results = compiler.CompileAssemblyFromFile (options, Path.Combine(path, "foo.cs"));

            if (results.Errors.HasErrors)
            {
                foreach (var error in results.Errors)
                    Console.WriteLine (error);
            }
            else
            {
                var types = results.CompiledAssembly.GetTypes ();

                var sb = new StringBuilder ();

                sb.AppendLine ("using System;");
                sb.AppendLine ("using System.Data;");
                sb.AppendLine ("using System.Data.SqlClient;");
                sb.AppendLine ("using Scaffolding;");

                sb.AppendLine ();
                sb.AppendLine ("namespace DbHelpers");
                sb.AppendLine ("{");

                sb.AppendLine ("public static class DbUtils", 1);
                sb.AppendLine("{", 1);

                foreach (var type in types)
                {
                    sb.AppendLine (MakeCreateUpdateDelete.CreateMethod (type, 2), 2);
                }                

                foreach (var type in types)
                {
                    if (type.IsInterface)
                        sb.AppendLine(MakeCreateUpdateDelete.Hyrdate (type, 2), 2);
                }

                sb.AppendLine ("}", 1);

                sb.AppendLine ("}");
                var sw = new StreamWriter (Path.Combine (path, "helpers.cs"));

                sw.Write (sb.ToString ());
                sw.Close ();
            }

        }
    }
}
