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
    public partial class SqlHelper
    {
        private bool stack_opened = false;
        private List<SqlUnit> stack = new List<SqlUnit>();

        private string _ConnectionString { get; set; }
        public string ConnectionString {
            get
            {
                if (_ConnectionString != null && _ConnectionString != "") return _ConnectionString;
                else return string.Format("data source={0};initial catalog={1};User Id={2};Password={3}", ServerName, DatabaseName, UserName, Password);
            }
            set
            {
                _ConnectionString = value;
            }
        }
        public string ServerName { get; set; }
        public string DatabaseName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        //private Dictionary<Type, object> DbSets = new Dictionary<Type, object>();

        //public IEnumerable<T> Query<T>() where T : class, new()
        //{
        //    Type type = typeof(T);
        //    if (!DbSets.ContainsKey(typeof(T)))
        //    {
        //        DbSets.Add(type, Get<T>(0).GetFirstTableList<T>());
        //    }
        //    return (IEnumerable<T>)DbSets[type];
        //}

        public SqlHelper()
        {

        }
        public SqlHelper(string connecting_string)
        {
            _ConnectionString = connecting_string;
        }
        public SqlHelper(string server, string database, string username, string password)
        {
            ServerName = server;
            DatabaseName = database;
            UserName = username;
            Password = password;
        }

        // Get
        private SqlResult<T> HGet<T>(string command, CommandType command_type, params SqlParameter[] sqlparams) where T : class, new()
        {
            return Execute<T>(command, command_type, false, sqlparams);
        }
        public SqlResult Get(string command, params SqlParameter[] sqlparams)
        {
            return Get(command, CommandType.Text, sqlparams);
        }
        public SqlResult Get(string command, CommandType command_type, params SqlParameter[] sqlparams)
        {
            return HGet<object>(command, command_type, sqlparams);
        }
        // Set
        public SqlResult Set(string command, params SqlParameter[] sqlparams)
        {
            return Set(command, CommandType.Text, sqlparams);
        }
        public SqlResult Set(string command, CommandType command_type, params SqlParameter[] sqlparams)
        {
            return Execute<object>(command, command_type, true, sqlparams);
        }
        //
        private SqlResult<T> Execute<T>(string command, CommandType command_type, bool execute_non_query, params SqlParameter[] sqlparams) where T : class, new()
        {
            if (stack_opened) { // 如果栈开启，则不执行，将sql执行信息放入栈中
                stack.Add(new SqlUnit { command = command, command_type = command_type, execute_non_query = execute_non_query, sqlparams = sqlparams });
                return null;
            }

            SqlCommand cmd = new SqlCommand();
            cmd.Connection = new SqlConnection(ConnectionString);
            cmd.CommandText = command;
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
            try
            {
                //while (cmd.Connection.State != ConnectionState.Closed) {} // 等待其他线程请求完毕再继续执行，否则一直阻塞
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
                    return new SqlResult<T>(null, count, return_value, output_value);
                }
                else
                {
                    SqlDataAdapter sda = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    sda.Fill(ds);
                    cmd.Parameters.Clear();
                    return new SqlResult<T>(ds, -1, null, null);
                }
            }
            catch (Exception err)
            {
                if (cmd.Connection.State != ConnectionState.Closed) cmd.Connection.Close();
                throw new Exception("数据库操作错误：" + err.Message + "。SQL语句：" + command);
            }
        }
        //
        public static List<T> DataTableToList<T>(DataTable dt) where T : class, new()
        {
            Dictionary<string, FieldAttribute> fas = GetParsedModel(typeof(T)).StorageableFields;
            // 构造列表
            List<T> ts = new List<T>();// 定义集合
            foreach (DataRow dr in dt.Rows)
            {
                T t = new T();
                foreach (string key in fas.Keys)
                {
                    FieldAttribute fa = fas[key];
                    object value = dr[fa.FieldName];
                    if (value != DBNull.Value)
                    {
                        fa.PropertyInfo.SetValue(t, ConvertEx.ChangeType((IConvertible)value, fa.PropertyInfo.PropertyType), null);
                    }
                }
                ts.Add(t);
            }
            return ts;
        }

        public void OpenStack()
        {
            stack_opened = true;
        }

        public void CloseStack()
        {
            stack_opened = false;
        }

        public void RunStack()
        {
            CloseStack();
            string all_command = "";
            if (stack.Count == 0) return;
            for(int i = 0; i < stack.Count; i++)
            {
                SqlUnit su = stack[i];
                all_command += su.command + ";";
                //this.Execute<Object>(su.command, su.command_type, su.execute_non_query, su.sqlparams);
            }
            this.Set(all_command);
        }

        public SqlParameter CreateSqlParameter(string name, SqlDbType dbtype)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = name;
            param.SqlDbType = dbtype;
            return param;
        }
        public SqlParameter CreateSqlParameter(string name, object value)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = name;
            param.Value = value;
            return param;
        }
        public SqlParameter CreateSqlParameter(string name, SqlDbType dbtype, object value)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = name;
            param.SqlDbType = dbtype;
            param.Value = value;
            return param;
        }
        public SqlParameter CreateSqlParameter(string name, SqlDbType dbtype, ParameterDirection direct)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = name;
            param.SqlDbType = dbtype;
            param.Direction = direct;
            return param;
        }
        public SqlParameter CreateSqlParameter(string name, SqlDbType dbtype, object value, ParameterDirection direct)
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
        private Dictionary<string, object> _dictionary = null;
        private object _return = null;
        private object _output = null;
        private int _effected_line_count = -1;

        // Get方法设置的值
        public DataSet DataSet { get { return _set; } } // 数据集
        public DataTable FirstTable { get { return _first_datatable; } } // 第一个数据表
        public object ScalarValue { get { return _scalar; } } // 第一个值
        public Dictionary<string, object> Dictionary { get { return _dictionary; } } // 第一行数据转为字典形式

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
                this._dictionary = new Dictionary<string, object>();
                if (ds.Tables[0].Rows.Count > 0 && ds.Tables[0].Columns.Count > 0)
                {
                    object temp = ds.Tables[0].Rows[0][0];
                    this._scalar = temp == DBNull.Value ? null : temp;
                    foreach(DataColumn column in ds.Tables[0].Columns)
                    {
                        temp = ds.Tables[0].Rows[0][column.ColumnName];
                        _dictionary.Add(column.ColumnName, temp == DBNull.Value ? null : temp);
                    }
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
    }

    public class SqlResult<T> : SqlResult where T : class, new()
    {
        public SqlResult(DataSet ds, int effectedLineCount, object return_value, object output_value) :base(ds, effectedLineCount, return_value, output_value)
        {

        }

        public List<T> GetFirstTableList()
        {
            return GetFirstTableList<T>();
        }

        public T GetFirstEntity()
        {
            return GetFirstEntity<T>();
        }
    }
    #endregion

    class SqlUnit
    {
        public string command { get; set; }
        public CommandType command_type { get; set; }
        public bool execute_non_query { get; set; }
        public SqlParameter[] sqlparams { get; set; }
    }

    static class ConvertEx
    {
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
