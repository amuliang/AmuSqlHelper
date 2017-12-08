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

        public static T GetById<T>(string table_name, string id) where T : class, new()
        {
            string sql_str = string.Format("select * from [{0}] where [id]='{1}'", table_name, id);
            return SqlHelper.Get(sql_str).GetFirstEntity<T>();
        }

        public static T GetById<T>(string table_name, int id) where T : class, new()
        {
            return GetById<T>(table_name, id.ToString());
        }

        public static int Insert<T>(string table_name, T obj) where T : class, new()
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
                notLast = i < len - 1;
                columns += "[" + pi.Name + "]" + (notLast ? "," : "");
                values += "'" + pi.GetValue(obj) + "'" + (notLast ? "," : "");
            }
            string sql_str = string.Format("insert into [{0}] ({1}) values ({2});select @@IDENTITY", table_name, columns, values);
            SqlResult sr = SqlHelper.Set(sql_str);
            if((pi = typeof(T).GetProperty("id")) != null ||
                (pi = typeof(T).GetProperty("Id")) != null ||
                (pi = typeof(T).GetProperty("ID")) != null)
            {
                pi.SetValue(sr.ScalarValue, pi.PropertyType);
            }
            return sr.EffectedLineCount;
        }

        public static int Update<T>(string table_name, T obj) where T : class, new()
        {
            PropertyInfo pi;
            string id = "";
            if ((pi = typeof(T).GetProperty("id")) != null ||
                   (pi = typeof(T).GetProperty("Id")) != null ||
                   (pi = typeof(T).GetProperty("ID")) != null)
            {
                id = pi.GetValue(obj).ToString();
            }
            if (id == "") return 0;

            PropertyInfo[] properties = typeof(T).GetProperties();// 获得此模型的公共属性
            string keyvalues = "";
            int len = properties.Length;
            bool notLast;
            for (int i = 0; i < len; i++)
            {
                pi = properties[i];
                notLast = i < len - 1;
                keyvalues += string.Format("[{0}]='{1}'", pi.Name, pi.GetValue(obj)) + (notLast ? "," : "");
            }
            string sql_str = string.Format("update [{0}] set {1} where [id]='{2}'", table_name, keyvalues, id);
            return SqlHelper.Set(sql_str).EffectedLineCount;
        }

        public static int Delete(string table_name, int id)
        {
            string sql_str = string.Format("delete from [{0}] where [id]='{1}'", table_name, id);
            return SqlHelper.Set(sql_str).EffectedLineCount;
        }
    }
}