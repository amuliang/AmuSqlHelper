using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;

namespace AmuTools
{
    /// <summary>
    /// HelperBLL 的摘要说明
    /// </summary>
    public class SqlHelperEx
    {
        /*********************** ***********************************************************************************/
        public static DataTable GetPageDataTable(string table_name, string condition, string order_by, int skip, int take)
        {
            return GetPageData(table_name, condition, order_by, skip, take).FirstTable;
        }

        public static List<T> GetPageDataList<T>(string table_name, string condition, string order_by, int skip, int take) where T : class, new()
        {
            return GetPageData(table_name, condition, order_by, skip, take).GetFirstTableList<T>();
        }

        private static SqlResult GetPageData(string table_name, string condition, string order_by, int skip, int take)
        {
            SqlParameter[] param = new SqlParameter[]
            {
            SqlHelper.CreateSqlParameter("@table_name", SqlDbType.NVarChar, table_name),
            SqlHelper.CreateSqlParameter("@condition", SqlDbType.NVarChar, condition),
            SqlHelper.CreateSqlParameter("@order_by", SqlDbType.NVarChar, order_by),
            SqlHelper.CreateSqlParameter("@skip", SqlDbType.Int, skip),
            SqlHelper.CreateSqlParameter("@take", SqlDbType.Int, take)
            };
            return SqlHelper.Get("sp_amu_getPageData", CommandType.StoredProcedure, param);
        }

        public static int GetCount(string table_name, string condition)
        {
            string sql_str = string.Format("select count(*) from {0}", table_name);
            if (condition != null && condition != "")
            {
                sql_str += " where " + condition;
            }
            return (int)SqlHelper.Get(sql_str).ScalarValue;
        }

        public static PostPage<T> GetPostPage<T>(string table_name, string condition, string order_by, int page, int count) where T : class, new()
        {
            return new PostPage<T>()
            {
                page = page,
                count = count,
                total = GetCount(table_name, condition),
                condition = condition,
                orderby = order_by,
                list = GetPageData(table_name, condition, order_by, (page-1)*count, count).GetFirstTableList<T>()
            };
        }

        public static T GetById<T>(string table_name, string id, string id_name = "id") where T : class, new()
        {
            string sql_str = string.Format("select * from [{0}] where [{1}]='{2}'", table_name, id_name, id);
            return SqlHelper.Get(sql_str).GetFirstEntity<T>();
        }
        public static T GetById<T>(string id) where T : class, new()
        {
            return GetById<T>(GetTableName<T>(), id, GetPrimaryKey<T>());
        }
        public static T GetById<T>(int id) where T : class, new()
        {
            return GetById<T>(GetTableName<T>(), id, GetPrimaryKey<T>());
        }
        public static T GetById<T>(string table_name, int id, string id_name = "id") where T : class, new()
        {
            return GetById<T>(table_name, id.ToString(), id_name);
        }

        public static int Insert<T>(string table_name, T obj, bool identity_insert = true, string id_name = "id") where T : class, new()
        {
            PropertyInfo[] properties = typeof(T).GetProperties();// 获得此模型的公共属性
            string columns = "";
            string values = "";
            int len = properties.Length;
            PropertyInfo pi;
            bool notLast;
            for (int i = 0; i < len; i++)
            {
                pi = properties[i];
                if (pi.Name == id_name && identity_insert == true) continue;
                notLast = i < len - 1;
                columns += "[" + pi.Name + "]" + (notLast ? "," : "");
                values += "'" + pi.GetValue(obj) + "'" + (notLast ? "," : "");
            }
            string sql_str = string.Format("insert into [{0}] ({1}) values ({2});select @@IDENTITY", table_name, columns, values);
            SqlResult sr = SqlHelper.Get(sql_str);
            if(identity_insert == true)
            {
                if ((pi = typeof(T).GetProperty(id_name)) != null)
                {
                    pi.SetValue(obj, Convert.ChangeType(sr.ScalarValue, pi.PropertyType));
                }
            }
            return sr.EffectedLineCount;
        }
        public static int Insert<T>(T obj) where T : class, new()
        {
            return Insert<T>(GetTableName<T>(), obj, GetIdentityInsert<T>(), GetPrimaryKey<T>());
        }

        public static int Update<T>(string table_name, T obj, string id_name = "id") where T : class, new()
        {
            PropertyInfo pi;
            string id = "";
            if ((pi = typeof(T).GetProperty(id_name)) != null)
            {
                id = pi.GetValue(obj).ToString();
            }
            if (id == "") throw new Exception(string.Format("更新表数据时，未向实例提供主键值，表：{0}，主键：{1}", typeof(T).Name, id_name));

            PropertyInfo[] properties = typeof(T).GetProperties();// 获得此模型的公共属性
            string keyvalues = "";
            int len = properties.Length;
            bool notLast;
            for (int i = 0; i < len; i++)
            {
                pi = properties[i];
                if (pi.Name == id_name) continue;
                notLast = i < len - 1;
                keyvalues += string.Format("[{0}]='{1}'", pi.Name, pi.GetValue(obj)) + (notLast ? "," : "");
            }
            string sql_str = string.Format("update [{0}] set {1} where [{2}]='{3}'", table_name, keyvalues, id_name, id);
            return SqlHelper.Set(sql_str).EffectedLineCount;
        }
        public static int Update<T>(T obj) where T : class, new()
        {
            return Update<T>(GetTableName<T>(), obj, GetPrimaryKey<T>());
        }

        public static int Delete(string table_name, int id, string id_name = "id")
        {
            string sql_str = string.Format("delete from [{0}] where [{1}]='{2}'", table_name, id_name, id);
            return SqlHelper.Set(sql_str).EffectedLineCount;
        }
        public static int Delete<T>(int id)
        {
            return Delete(GetTableName<T>(), id, GetPrimaryKey<T>());
        }
        public static int Delete(string table_name, string id, string id_name = "id")
        {
            string sql_str = string.Format("delete from [{0}] where [{1}]='{2}'", table_name, id_name, id);
            return SqlHelper.Set(sql_str).EffectedLineCount;
        }
        public static int Delete<T>(string id)
        {
            return Delete(GetTableName<T>(), id, GetPrimaryKey<T>());
        }

        private static string GetTableName<T>()
        {
            FieldInfo f = typeof(T).GetField("TableName");
            if (f == null) throw new Exception("模型类" + typeof(T).Name + "未声明TableName属性，public static string TableName = \"tablename\";");
            return f.GetValue(null).ToString();
        }
        private static string GetPrimaryKey<T>()
        {
            FieldInfo f = typeof(T).GetField("PrimaryKey");
            if (f == null) throw new Exception("模型类" + typeof(T).Name + "未声明PrimaryKey属性，public static string PrimaryKey = \"tablename\";");
            return f.GetValue(null).ToString();
        }
        private static bool GetIdentityInsert<T>()
        {
            FieldInfo f = typeof(T).GetField("IdentityInsert");
            if (f == null) throw new Exception("模型类" + typeof(T).Name + "未声明IdentityInsert属性，public static bool IdentityInsert = true;");
            return (bool)f.GetValue(null);
        }
    }
}