using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmuTools;
using System.Reflection;

namespace AmuSqlHelper
{
    class Program
    {
        static void Main(string[] args)
        {
            BindingFlags flag = BindingFlags.Static | BindingFlags.NonPublic;
            PropertyInfo f_key = typeof(Article).GetProperty("TableName");
            FieldInfo f_key2 = typeof(Article).GetField("TableName", BindingFlags.Static);
            string obj = f_key.GetValue(null).ToString();
        }
    }

    public class Article
    {
        public static string TableName = "article";
        public static string PrimaryKey = "id";
        public int id { get; set; }
        public string author { get; set; }
        public string create_time { get; set; }
        public int status { get; set; }
        public int is_delete { get; set; }
    }
}
