using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Management;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Sdk;

namespace Sql_Object_Generator
{
    static class Utils
    {

        static public IEnumerable<Table> GetParentTables(this Table table)
        {
            var db = table.Parent;
            var pks = table.Columns.ToEnumerable ().Where (x => x.InPrimaryKey && x.IsForeignKey);
            var fks = table.ForeignKeys.ToEnumerable ();

            var keys = from p in pks
                       let f = fks.Where (x => x.Columns.Contains (p.Name)).ToList ()
                       where f.Any ()
                       select new { p, f = f[0] };

            var list = new List<Table> ();

            foreach (var k in keys)
                list.Add (db.Tables[k.f.ReferencedTable]);

            return list;

        }


        public static bool IsReadOnly(this Column column)
        {
            return column.Computed || column.Identity;
        }


        public static Type GetClrType(this Column column)
        {
            return column.DataType.GetClrType (column.Nullable);
        }


        public static Type GetClrType(this DataType SqlType, bool nullable = false)
        {
            var name = SqlType.Name.ToUpper ();

            switch (name)
            {
                case "INT":
                    return nullable ? typeof (int?) : typeof (int);

                case "BYTE":
                    return nullable ? typeof (byte?) : typeof (byte);

                case "FLOAT":
                    return nullable ? typeof (double?) : typeof (double);

                case "TINYINT":
                    return nullable ? typeof (byte?) : typeof (byte);

                case "SMALLINT":
                    return nullable ? typeof (short?) : typeof (short);

                case "DECIMAL":
                    return nullable ? typeof (decimal?) : typeof (decimal);



                case "CHAR":
                    return SqlType.MaximumLength == 1 ? (nullable ? typeof (char?) : typeof (char)) : typeof (string);

                case "VARCHAR":
                    return typeof (string);

                case "VARCHAR(MAX)":
                    return typeof (string);

                case "NVARCHAR":
                    return typeof (string);

                case "NVARCHAR(MAX)":
                    return typeof (string);

                case "TEXT":
                    return typeof (string);

                case "NTEXT":
                    return typeof (string);



                case "DATETIME":
                    return nullable ? typeof (DateTime?) : typeof (DateTime);

                case "SMALLDATETIME":
                    return nullable ? typeof (DateTime?) : typeof (DateTime);



                case "bit":
                    return nullable ? typeof (bool?) : typeof (bool);

                case "UNIQUEIDENTIFIER":
                    return nullable ? typeof (Guid?) : typeof (Guid);

                default:
                    return typeof (object);

            }
        }

        public static string GetFriendlyClrName(this Type type)
        {
            
            
            
            bool nullable = false;
            Type x = null;
            string typeName = null;

            if (type.IsGenericType && type.GetGenericTypeDefinition () == typeof (Nullable<>))
            {
                nullable = true;
                x = type.GetGenericArguments ()[0];
            }
            else
            {
                x = type;
            }

            typeName = GetCSharpTypeAlias (x.FullName);

            return typeName + (nullable ? "?" : "");


        }

        static readonly Dictionary<string, string> CSharpTypeAliases = new Dictionary<string, string>
            {
                {"String", "string"},
                {"Char", "char"},
                {"Boolean", "bool"},
                {"Int32", "int"},
                {"Int16", "short"},                
                {"Byte", "byte"},
                {"Double", "double"},
                {"Decimal", "decimal"}
            };

        private static string GetCSharpTypeAlias(string Name)
        {
            var xs = Name.Split ('.');

            if (xs.Length == 2 && xs[0] == "System" && CSharpTypeAliases.ContainsKey (xs[1])) //inefficient.
            {
                Name = CSharpTypeAliases[xs[1]];
            }


            return Name;

        }


        public static bool Implies(this bool antecedent, bool consequent)
        {
            return !antecedent || consequent;
        }


        public static IEnumerable<StoredProcedureParameter> ToEnumerable(this StoredProcedureParameterCollection xs)
        {
            foreach (StoredProcedureParameter @param in xs)
                yield return @param;
        }


        public static IEnumerable<Column> ToEnumerable(this ColumnCollection xs)
        {
            foreach (Column column in xs)
                yield return column;
        }

        public static IEnumerable<ForeignKeyColumn> ToEnumerable(this ForeignKeyColumnCollection xs)
        {
            foreach (ForeignKeyColumn column in xs)
                yield return column;
        }

        public static IEnumerable<ForeignKey> ToEnumerable(this ForeignKeyCollection xs)
        {
            foreach (ForeignKey fk in xs)
                yield return fk;
        }

        public static IEnumerable<Table> ToEnumerable(this TableCollection xs)
        {
            foreach (Table x in xs)
                yield return x;
        }
    }
}
