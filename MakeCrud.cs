using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Management.Smo;
using System.Reflection;
using System.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Data;
using CodeGeneration;
using Scaffolding;

namespace Sql_Object_Generator
{
    static class MakeCreateUpdateDelete
    {

        static readonly Dictionary<Type, string> CRL_TO_SQL = new Dictionary<Type, string> ()
        {
            { typeof(string), "GetString"  },
            
            
            { typeof(int), "GetInt32" },
            { typeof(int?), "GetInt32" },

            { typeof(bool), "GetBoolean" },
            { typeof(bool?), "GetBoolean" },
            
            { typeof(double), "GetDouble" },
            { typeof(double?), "GetDouble" },
            
            { typeof(decimal), "GetDecimal" },
            { typeof(decimal?), "GetDecimal" },
            
            { typeof(float), "GetFloat" },
            { typeof(float?), "GetFloat" },
                        
            { typeof(long), "GetInt64" },
            { typeof(long?), "GetInt64" },
            
            { typeof(short), "GetInt16" },
            { typeof(short?), "GetInt16" },
            
            { typeof(byte), "GetByte"},
            { typeof(byte?), "GetByte"},

            { typeof(DateTime), "GetDateTime" },
            { typeof(DateTime?), "GetDateTime" },
            
            { typeof(object), "GetValue" },
            
            {typeof(char), "GetChar"},
            {typeof(char?), "GetChar"},

            {typeof(Guid), "GetGuid"},
            {typeof(Guid?), "GetGuid"},
            
        };

        static string GetReaderMethod(Type type)
        {
            var method = "GetValue";

            if (CRL_TO_SQL.ContainsKey (type))
                method = CRL_TO_SQL[type];

            return method;
        }
       
        public static string Hyrdate(Type type, int n = 0)
        {

            var sb = new StringBuilder ();


            sb.AppendLine (string.Format("public static void Hydrate(this {0} obj, IDataReader Reader)", type.Name));
            sb.AppendLine ("{", n);

            var conditionals    = new Stack<StringBuilder> ();
            var un_conditionals = new Stack<StringBuilder> ();
            

            foreach (var property in type.GetProperties ())
            {

                string colName = property.GetCustomAttributes(true).Where (x => x is ColumnAttribute).Select (x => (ColumnAttribute)x).First ().Name;

                var m = n;
                Stack<StringBuilder> sbs = null;

                if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition () == typeof (Nullable<>))
                {
                    sbs = conditionals;
                    sbs.Push (new StringBuilder ());

                    sbs.Peek ().AppendLine (string.Format ("if(!Reader.IsDBNull(Reader.GetOrdinal(\"{0}\")))", colName), m++ + 1);
                }
                else
                {
                    sbs = un_conditionals;
                    sbs.Push (new StringBuilder ());
                }

                var s = string.Format ("obj.{0} = Reader.{1}(Reader.GetOrdinal(\"{2}\"));", property.Name, GetReaderMethod (property.PropertyType), colName);

                sbs.Peek().AppendLine (s, m + 1);              


                
            }

            foreach (var sb_ in un_conditionals.Reverse())
                sb.Append (sb_.ToString ());

            sb.AppendLine ();

            foreach(var sb_ in conditionals.Reverse())
                sb.AppendLine(sb_.ToString());

            sb.AppendLine ("}", n);

            return sb.ToString ();

        }
        
        public static string CreateMethod(Type type, int n = 0)
        {
            var sb = new StringBuilder ();

            sb.AppendLine ("public static void PopulateCommand(this " + type.Name + " obj , SqlCommand Command)");
            sb.AppendLine ("{", n);

            foreach (var prop in type.GetProperties ())
            {
                sb.AppendLine ("\t" + MakeParameter (prop), n);
            }            

            sb.AppendLine ("}", n);

            return sb.ToString ();

        }



        public static string CreateStandardImplementation(this Type type, int n = 0)
        {
            var sb = new StringBuilder ();


            var types = type.GetInterfaces ().Where (x => x.GetCustomAttributes (typeof (RelationAttribute), false).Length > 0).Union (new[] { type });

            foreach (var t in types)
            {

                var relName = (RelationAttribute) t.GetCustomAttributes (typeof (RelationAttribute), false)[0];
                
                sb.AppendLine (string.Format ("public partial class {0} : {2}, {1}", relName.RelationText, t.Name, "DataContext"), n);
                sb.AppendLine ("{", n);



                foreach (var property in t.GetProperties ().Where (x => x.GetCustomAttributes (typeof (ColumnAttribute), false).Length > 0))
                {
                    
                    var propName = property.Name.Split('_').Aggregate("", (x, y) => x + y.Capitalize() );
                    var colAttr = ((ColumnAttribute)property.GetCustomAttributes (typeof (ColumnAttribute), false)[0]);
                    var prv_fld = " m__" + property.Name;

                    sb.AppendLine (property.PropertyType.GetFriendlyClrName () + prv_fld + ";", n + 1); 
                    sb.AppendLine (string.Format ("public {0} {1}", property.PropertyType.GetFriendlyClrName(), propName), n + 1);
                    sb.AppendLine ("{", n + 1);

                    sb.AppendLine ("get", n + 2);
                    sb.AppendLine ("{", n + 2);
                    sb.AppendLine ("return " + prv_fld + ";", n + 3);

                    sb.AppendLine ("}", n + 2);


                    if (!colAttr.ReadOnly)
                    {
                        sb.AppendLine ("set", n + 2);
                        sb.AppendLine ("{", n + 2);
                        sb.AppendLine (prv_fld + " = value;", n + 3);

                        sb.AppendLine ("}", n + 2);
                    }


                    sb.AppendLine ("}", n + 1);

                    sb.AppendLine ();


                    sb.AppendLine (property.PropertyType.GetFriendlyClrName () + " " + t.Name + "." + property.Name, n + 1);
                    sb.AppendLine ("{", n + 1);

                    sb.AppendLine ("get", n + 2);
                    sb.AppendLine ("{", n + 2);
                    sb.AppendLine ("return " + prv_fld + ";", n + 3);
                    sb.AppendLine ("}", n + 2);
                    sb.AppendLine ();


                    sb.AppendLine ("set", n + 2);
                    sb.AppendLine ("{", n + 2);
                    sb.AppendLine (prv_fld + " = value;", n + 3);
                    sb.AppendLine ("}", n + 2);

                    sb.AppendLine ("}", n + 1);
                    sb.AppendLine ();


                }



                sb.AppendLine ("}", n);

            }


            return sb.ToString ();
        }

        public static string MakeParameter(PropertyInfo pi)
        {
            var attrs = pi.GetCustomAttributes (typeof (ColumnAttribute), false).Select (x => x as ColumnAttribute);

            string name = null;

            foreach (var attr in attrs)
            {
                name = "@" + attr.Name;
            }

            return "Command.Parameters.AddWithValue(\"" + name + "\", " + "obj." + pi.Name + ");";
        }



    }


    class TableInterface : Interface
    {
        Table table;
        
        public TableInterface(Table table) : this(table.Name.Split(' ').Select(x => x.Trim()).Aggregate("", (x,y) => x + "_" + y ))
        {
            this.table = table;
            this.Properties.AddRange 
                (table.Columns.ToEnumerable ()
                    .Where(x => !x.Computed) /* We're building an interface so we don't need computed columns */
                    .Select (x => new SqlProperty (x)));
        }

        private TableInterface(string Name) : base(Name) { }

        public override string ToString(int n)
        {
            var sb = new StringBuilder ();

            sb.AppendLine (string.Format ("[Relation(\"{0}\")]", table.Name));
            sb.AppendLine ("public partial interface _" + Name, n);
            sb.AppendLine ("{", n);

            foreach (var method in this.Methods)
                sb.AppendLine (method.ToString(n));

            foreach (var property in this.Properties)
                sb.AppendLine (property.ToString (n));

            sb.AppendLine ("}", n);

            return sb.ToString ();
        }
    }
    
    class SqlProperty : CodeGeneration.Property
    {
        private SqlProperty(string Name, Type Type, Accessiblity Accessiblity = null) : base(Name, Type, Accessiblity) {}
        
        Column column;

        public SqlProperty(Column column) : this(column.Name, column.GetClrType(), Accessiblity.@public)
        {
            this.column = column;
            this.canRead = true;
            this.canWrite = !column.IsReadOnly ();
        }

        public override string ToString(int n)
        {
            var sb = new StringBuilder();


            var args = new[] { "\"" + column.Name + "\"", column.IsReadOnly ().ToString ().ToLower (), column.InPrimaryKey.ToString ().ToLower () }.Join (",");

            var columnAttribute = "[Column(" + args + ")]";

            sb.AppendLine (columnAttribute, n + 1);

            sb.AppendLine (MemberType.GetFriendlyClrName() + " " + Name, n + 1);
            sb.AppendLine ("{", n + 1);
            sb.AppendLine ("get;", n + 2);
            sb.AppendLine ("set;", n + 2);

            sb.AppendLine ("}", n + 1);

            return sb.ToString ();
        }
    }
}

