using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Data.SqlClient;

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
    }
}