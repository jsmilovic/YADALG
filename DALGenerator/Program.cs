using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Data.Entity.Design.PluralizationServices;
using System.Data.Entity.Design;
using System.Globalization;
namespace DALGenerator
{
    class Program
    {
        
        public const string SQL_DATA_CONNECTION = "data source=localhost; Integrated Security=True; Initial Catalog=";
        static void Main(string[] args)
        {

            PluralizationService pls = PluralizationService.CreateService(CultureInfo.CurrentCulture);
            SqlConnection conn;
            SqlConnection secondConn; //Need a second connection because the first stays open.
        
            //Default to small groups if there is no argument since this is what I wrote it for.
            if (args.Length < 1)
            {
                conn = new SqlConnection(SQL_DATA_CONNECTION + "SmallGroups;");
                secondConn = new SqlConnection(SQL_DATA_CONNECTION + "SmallGroups;");
            }
        
            else
            {
                conn = new SqlConnection(SQL_DATA_CONNECTION + args[0] + ";");
                secondConn = new SqlConnection(SQL_DATA_CONNECTION + args[0] + ";");
            }
             
            conn.Open();
            secondConn.Open();
            SqlCommand command = new SqlCommand();
            SqlCommand secondCommand = new SqlCommand();

            secondCommand.Connection = secondConn;
            command.CommandText = "Select distinct table_name from information_schema.columns";
            command.Connection = conn;
            
            List<string> TableNames = new List<string>();
            SqlDataReader reader = command.ExecuteReader();
            SqlDataReader secondReader;
        
            //Get all the table names from the database
            while (reader.Read())
            {
                TableNames.Add(reader[0].ToString());
            }
            
            reader.Close();
            
            //Target directory for files
            if (!Directory.Exists("c:\\DAL"))
                Directory.CreateDirectory("C:\\DAL");


            //Loop over the table names to create files for them
            foreach (string table in TableNames)
            {
                TextWriter tw = new StreamWriter("c:\\DAL\\" + pls.Singularize(table) + ".cs");
                tw.WriteLine("using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Web;\nusing System.ComponentModel.DataAnnotations;\n");                
                tw.WriteLine("namespace SmallGroups.Models ");
                tw.WriteLine("{");
                tw.WriteLine("\tpublic class " + pls.Singularize(table) + " \n\t{");

                //Need to get all the columns for a given table so we can write them into the DAL
                command.CommandText = string.Format(@"select * from information_schema.columns where table_name='{0}'",table);
                reader = command.ExecuteReader();
                string DataType="";
                string IsNullable = "";
            
                //Loop through the column names
                while (reader.Read())
                {
                    DataType=reader["DATA_TYPE"].ToString();
                    IsNullable = reader["IS_NULLABLE"].ToString();
                    tw.Write("\t\tpublic ");

                    //This block converts the SQL Data type to the C# Datatype
                    if (DataType == "int")
                    {
                        tw.Write("int");
                    }
                    if ((DataType == "datetime") || (DataType == "date") || (DataType == "time"))
                    {
                        tw.Write("DateTime");
                    }
                    if (DataType == "varchar")
                    {
                        tw.Write("string");
                    }
                    if (DataType == "char")
                    {
                        tw.Write("string");
                    }
                    if (DataType == "bit")
                    {
                        tw.Write("bool");
                    }

                    //If it's nullable in the database we need a nullable type
                    if (IsNullable == "YES" && ((DataType != "char") && DataType != "varchar") )
                    {
                        tw.Write("?");
                    }

                    tw.Write(" ");
                    tw.WriteLine(reader["Column_Name"].ToString() + " { get; set; }");

                }
                
                reader.Close();

                //Building the information for the relationships to other tables
                command.CommandText = string.Format(@"select Column_Name, rc.Constraint_Name
                    from INFORMATION_SCHEMA.CONSTRAINT_TABLE_USAGE tu
                    inner join INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                    on tu.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
                    inner join INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE cc
                    on rc.CONSTRAINT_NAME = cc.CONSTRAINT_NAME
                    inner join INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    on rc.CONSTRAINT_NAME= tc.CONSTRAINT_NAME
                    where tu.TABLE_NAME = '{0}'", table);
                reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    tw.Write("\n\t\t");
                    tw.WriteLine(String.Format(@"[ForeignKey(""{0}"")]",reader[0]));
                    
                    //This will allow me to get the related class, IE foreign key relationships
                    secondCommand.CommandText = string.Format(@"select TABLE_NAME from INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS  rc
                        inner join INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        on rc.UNIQUE_CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                        where rc.CONSTRAINT_NAME = '{0}'",reader[1].ToString());
                        secondReader = secondCommand.ExecuteReader();
                    string RelatedClassName="";
                
                    while (secondReader.Read())
                    {
        
                        //Convention for CF projects
                        if (!reader[0].ToString().EndsWith("SystemCodeId")) 
                        {
                            RelatedClassName=pls.Singularize(secondReader[0].ToString());
                        }
                        else 
                        {
                            RelatedClassName = reader[0].ToString().Substring(0,reader[0].ToString().Length - 12);
                        }
                    
                        tw.Write("\t\t");
                        
                        //Write out the virtual for the relationship
                        tw.WriteLine(string.Format("public virtual {0} {1} {{ get; set; }}", pls.Singularize(secondReader[0].ToString()),RelatedClassName));
                        
                    }
                    
                    secondReader.Close();
                }

                reader.Close();
                tw.WriteLine("\t}");                
                tw.WriteLine("}");
                tw.Close();

            }
        }
    }
}
