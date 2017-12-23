using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AmuTools
{
    #region 数据库辅助
    /*
        目的：
            1.得到查询结果
            2.得到影响行数
            3.得到存储过程返回值
            4.得到错误,暂时没写
    */
    public class SqlHelper
    {
        private static string connection_string = "data source=.;initial catalog=databasename;User Id=sa;Password=123456";// ConfigurationManager.ConnectionStrings["Constr"].ToString();
        private static SqlCommand sql_command = null;
        public static string ConnectionString { get { return connection_string; } set { connection_string = value;sql_command = null; } }
        // Get
        public static SqlResult Get(string commond, params SqlParameter[] sqlparams)
        {
            return Get(commond, CommandType.Text, sqlparams);
        }
        public static SqlResult Get(string commond, CommandType command_type, params SqlParameter[] sqlparams)
        {
            return Execute(commond, command_type, false, sqlparams);
        }
        // Set
        public static SqlResult Set(string commond, params SqlParameter[] sqlparams)
        {
            return Set(commond, CommandType.Text, sqlparams);
        }
        public static SqlResult Set(string commond, CommandType command_type, params SqlParameter[] sqlparams)
        {
            return Execute(commond, command_type, true, sqlparams);
        }
        //
        private static SqlResult Execute(string commond, CommandType command_type, bool execute_non_query, params SqlParameter[] sqlparams)
        {
            if (sql_command == null)
            {
                sql_command = new SqlCommand();
                sql_command.Connection = new SqlConnection(ConnectionString);
            }
            SqlCommand cmd = sql_command;
            cmd.CommandText = commond;
            cmd.CommandType = command_type;
            // 添加参数，如果没有return值，则需要手动添加之
            string return_value_name = "";
            if (sqlparams != null)
            {
                foreach (SqlParameter param in sqlparams)
                {
                    if (param.Direction == ParameterDirection.ReturnValue) return_value_name = param.ParameterName;
                    cmd.Parameters.Add(param);
                }
            }
            if (return_value_name == "")
            {
                SqlParameter param = new SqlParameter();
                param.Direction = ParameterDirection.ReturnValue;
                cmd.Parameters.Add(param);
            }
            // 开始获取
            if (execute_non_query)
            {
                cmd.Connection.Open();
                int count = cmd.ExecuteNonQuery();
                cmd.Connection.Close();
                object return_value = null;
                object output_value = null;
                foreach (SqlParameter param in cmd.Parameters)
                {
                    if (param.Direction == ParameterDirection.ReturnValue) return_value = param.Value;
                    if (param.Direction == ParameterDirection.Output) output_value = param.Value;
                }
                cmd.Parameters.Clear();
                return new SqlResult(null, count, return_value, output_value);
            }
            else
            {
                SqlDataAdapter sda = new SqlDataAdapter(cmd);
                DataSet ds = new DataSet();
                sda.Fill(ds);
                cmd.Parameters.Clear();
                return new SqlResult(ds, -1, null, null);
            }
        }
        //
        public static List<T> DataTableToList<T>(DataTable dt) where T : class, new()
        {
            return dt == null ? null : ConvertEx.DataTableToList<T>(dt);
        }
        public static List<Dictionary<string, object>> DataTableToDicList(DataTable dt, string[] columns)
        {
            return dt == null ? null : ConvertEx.DataTableToDicList(dt, columns);
        }
        public static SqlParameter CreateSqlParameter(string name, SqlDbType dbtype)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = name;
            param.SqlDbType = dbtype;
            return param;
        }
        public static SqlParameter CreateSqlParameter(string name, object value)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = name;
            param.Value = value;
            return param;
        }
        public static SqlParameter CreateSqlParameter(string name, SqlDbType dbtype, object value)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = name;
            param.SqlDbType = dbtype;
            param.Value = value;
            return param;
        }
        public static SqlParameter CreateSqlParameter(string name, SqlDbType dbtype, ParameterDirection direct)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = name;
            param.SqlDbType = dbtype;
            param.Direction = direct;
            return param;
        }
        public static SqlParameter CreateSqlParameter(string name, SqlDbType dbtype, object value, ParameterDirection direct)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = name;
            param.SqlDbType = dbtype;
            param.Value = value;
            param.Direction = direct;
            return param;
        }
    }

    public class SqlResult
    {
        private DataSet _set = null;
        private DataTable _first_datatable = null;
        private object _scalar = null;
        private object _return = null;
        private object _output = null;
        private int _effected_line_count = -1;

        // Get方法设置的值
        public DataSet DataSet { get { return _set; } } // 数据集
        public DataTable FirstTable { get { return _first_datatable; } } // 第一个数据表
        public object ScalarValue { get { return _scalar; } } // 第一个值

        // Set方法设置的值
        public object ReturnValue { get { return _return; } } // 存储过程的返回值
        public object OutputValue { get { return _output; } } // 存储过程的输出值
        public int EffectedLineCount { get { return _effected_line_count; } } // 受影响的行数,当为查询时值为-1，当更新插入删除时值为受影响行数，如果发生回滚也为-1


        public SqlResult(DataSet ds, int effectedLineCount, object return_value, object output_value)
        {
            this._set = ds;
            if (ds != null && ds.Tables.Count > 0)
            {
                this._first_datatable = ds.Tables[0];
                if (ds.Tables[0].Rows.Count > 0 && ds.Tables[0].Columns.Count > 0)
                {
                    this._scalar = ds.Tables[0].Rows[0][0];
                }
            }
            this._return = return_value;
            this._output = output_value;
            this._effected_line_count = effectedLineCount;
        }

        public List<T> GetFirstTableList<T>() where T : class, new()
        {
            return SqlHelper.DataTableToList<T>(this.FirstTable);
        }

        public T GetFirstEntity<T>() where T : class, new()
        {
            return this.FirstTable != null && this.FirstTable.Rows.Count > 0 ? SqlHelper.DataTableToList<T>(this.FirstTable)[0] : null;
        }

        public List<Dictionary<string, object>> GetFirstTableDicList(string[] columns)
        {
            return SqlHelper.DataTableToDicList(this.FirstTable, columns);
        }

        public Dictionary<string, object> GetFirstDicEntity(string[] columns)
        {
            return this.FirstTable != null && this.FirstTable.Rows.Count > 0 ? SqlHelper.DataTableToDicList(this.FirstTable, columns)[0] : null;
        }

        public List<Dictionary<string, object>> GetFirstTableDicList<T>(int group_code = 0)
        {
            return SqlHelperEx.DataTableToDicList<T>(this.FirstTable, group_code);
        }

        public Dictionary<string, object> GetFirstDicEntity<T>(int group_code = 0)
        {
            return this.FirstTable != null && this.FirstTable.Rows.Count > 0 ? SqlHelperEx.DataTableToDicList<T>(this.FirstTable, group_code)[0] : null;
        }
    }
    #endregion

    static class ConvertEx
    {
        public static List<Dictionary<string, object>> DataTableToDicList(DataTable dt, string[] columns)
        {
            // 首先找到包含在datable中的列
            List<string> properties = new List<string>();
            foreach (string p in columns)
            {
                if (dt.Columns.Contains(p)) properties.Add(p);
            }

            // 构造dictionary
            List<Dictionary<string, object>> ts = new List<Dictionary<string, object>>();// 定义集合
            foreach (DataRow dr in dt.Rows)
            {
                Dictionary<string, object> t = new Dictionary<string, object>();
                foreach (string p in properties)
                {
                    t.Add(p, dr[p]);
                }
                ts.Add(t);
            }
            return ts;
        }

        public static List<T> DataTableToList<T>(DataTable dt) where T : class, new()
        {
            // 首先找到所有可以设置的属性，判断属性是否可写入，是否包含在datatable中
            PropertyInfo[] properties = typeof(T).GetProperties();// 获得此模型的公共属性
            List<PropertyInfo> writable_properties = new List<PropertyInfo>();
            foreach (PropertyInfo pi in properties)
            {
                if (pi.CanWrite && dt.Columns.Contains(pi.Name)) writable_properties.Add(pi);
            }

            // 构造列表
            List<T> ts = new List<T>();// 定义集合
            foreach (DataRow dr in dt.Rows)
            {
                T t = new T();
                foreach (PropertyInfo pi in writable_properties)
                {
                    object value = dr[pi.Name];
                    if (value != DBNull.Value)
                    {
                        pi.SetValue(t, ChangeType((IConvertible)value, pi.PropertyType), null);
                    }
                }
                ts.Add(t);
            }
            return ts;
        }

        public static object ChangeType(this IConvertible convertibleValue, Type t)
        {
            //if (string.IsNullOrEmpty(convertibleValue.ToString()))
            //{
            //    return default(T);
            //}
            if (!t.IsGenericType)
            {
                return Convert.ChangeType(convertibleValue, t);
            }
            else
            {
                Type genericTypeDefinition = t.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(Nullable<>))
                {
                    return Convert.ChangeType(convertibleValue, Nullable.GetUnderlyingType(t));
                }
            }
            throw new InvalidCastException(string.Format("Invalid cast from type \"{0}\" to type \"{1}\".", convertibleValue.GetType().FullName, t.FullName));
        }
    }
}
