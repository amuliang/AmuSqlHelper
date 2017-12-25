using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.IO;

namespace AmuTools
{
    /// <summary>
    /// SqlHelperEx 的摘要说明
    /// </summary>
    public partial class SqlHelper
    {
        #region 条件list数据
        public SqlResult GetPage<T>(string condition, string order_by, int skip, int take)
        {
            SqlParameter[] param = new SqlParameter[]
            {
                CreateSqlParameter("@table_name", SqlDbType.NVarChar, GetTableName<T>()),
                CreateSqlParameter("@condition", SqlDbType.NVarChar, condition),
                CreateSqlParameter("@order_by", SqlDbType.NVarChar, order_by),
                CreateSqlParameter("@skip", SqlDbType.Int, skip),
                CreateSqlParameter("@take", SqlDbType.Int, take)
            };
            return Get("sp_amu_getPageData", CommandType.StoredProcedure, param);
        }
        public SqlResult Get<T>(string condition = "", string order_by = "", int size = 1000) where T : class, new()
        {
            string order_by_str = order_by == "" ? "" : string.Format("orderby {0}", order_by);
            string condition_str = condition == "" ? "" : string.Format("where {0}", condition);
            string sql_str = string.Format("select top {0} * from [{1}] {2} {3}", size, GetTableName<T>(), condition_str, order_by_str);
            return Get(sql_str);
        }
        #endregion

        #region 特殊函数
        public int GetCount<T>(string condition)
        {
            string sql_str = string.Format("select count(*) from {0}", GetTableName<T>());
            if (condition != null && condition != "")
            {
                sql_str += " where " + condition;
            }
            return (int)Get(sql_str).ScalarValue;
        }
        public object GetMax<T>(string prop_name)
        {
            string sql_str = string.Format("select max({0}) from {1}", prop_name, GetTableName<T>());
            object result = Get(sql_str).ScalarValue;
            return result == null ? 0 : result;
        }
        public bool IsOne<T>(string condition)
        {
            string where_str = condition == null || condition == "" ? "" : string.Format(" where {0}", condition);
            string sql_str = string.Format("select count(*) from {0} {1}", GetTableName<T>(), where_str);
            return (int)Get(sql_str).ScalarValue == 1;
        }
        #endregion

        #region 增删改查
        public T GetById<T>(string id) where T : class, new()
        {
            ModelAttribute ma = GetModelAttribute<T>();
            string sql_str = string.Format("select * from [{0}] where [{1}]='{2}'", ma.TableName, ma.PrimaryKey, id);
            return Get(sql_str).GetFirstEntity<T>();
        }
        public T GetById<T>(int id) where T : class, new()
        {
            return GetById<T>(id.ToString());
        }
        
        private int Insert(Type type, object obj)
        {
            ModelAttribute ma = GetModelAttribute(type);
            PropertyInfo[] properties = GetStorageablePropertys(type);// 获得此模型的公共属性
            string columns = "";
            string values = "";
            int len = properties.Length;
            PropertyInfo pi;
            bool notLast;
            for (int i = 0; i < len; i++)
            {
                pi = properties[i];
                if (pi.Name == ma.PrimaryKey && ma.IdentityInsert == true) continue;
                notLast = i < len - 1;
                columns += "[" + pi.Name + "]" + (notLast ? "," : "");
                values += "'" + pi.GetValue(obj) + "'" + (notLast ? "," : "");
            }
            string sql_str = string.Format("insert into [{0}] ({1}) values ({2});select @@IDENTITY", ma.TableName, columns, values);
            SqlResult sr = Get(sql_str);
            if (ma.IdentityInsert == true)
            {
                if ((pi = type.GetProperty(ma.PrimaryKey)) != null)
                {
                    pi.SetValue(obj, Convert.ChangeType(sr.ScalarValue, pi.PropertyType));
                }
            }
            return sr.EffectedLineCount;
        }
        public int Insert<T>(T obj) where T : class, new()
        {
            return Insert(typeof(T), obj);
        }
        
        public int Update<T>(T obj) where T : class, new()
        {
            ModelAttribute ma = GetModelAttribute<T>();
            PropertyInfo pi;
            string id = "";
            if ((pi = typeof(T).GetProperty(ma.PrimaryKey)) != null)
            {
                id = pi.GetValue(obj).ToString();
            }
            if (id == "") throw new Exception(string.Format("更新表数据时，未向实例提供主键值，表：{0}，主键：{1}", typeof(T).Name, ma.PrimaryKey));

            PropertyInfo[] properties = GetStorageablePropertys<T>();// 获得此模型的公共属性
            string keyvalues = "";
            int len = properties.Length;
            bool notLast;
            for (int i = 0; i < len; i++)
            {
                pi = properties[i];
                if (pi.Name == ma.PrimaryKey) continue;
                notLast = i < len - 1;
                keyvalues += string.Format("[{0}]='{1}'", pi.Name, pi.GetValue(obj)) + (notLast ? "," : "");
            }
            string sql_str = string.Format("update [{0}] set {1} where [{2}]='{3}'", ma.TableName, keyvalues, ma.PrimaryKey, id);
            return Set(sql_str).EffectedLineCount;
        }
        
        public int Delete<T>(int id)
        {
            return Delete<T>(id.ToString());
        }
        public int Delete<T>(string id)
        {
            ModelAttribute ma = GetModelAttribute<T>();
            string sql_str = string.Format("delete from [{0}] where [{1}]='{2}'", ma.TableName, ma.PrimaryKey, id);
            return Set(sql_str).EffectedLineCount;
        }
        #endregion

        #region Attribute相关函数
        public string GetTableName<T>()
        {
            return GetTableName(typeof(T));
        }
        private static string GetTableName(Type type)
        {
            return GetModelAttribute(type).TableName;
        }
        public string GetPrimaryKey<T>()
        {
            return GetModelAttribute<T>().PrimaryKey;
        }
        public bool GetIdentityInsert<T>()
        {
            return GetModelAttribute<T>().IdentityInsert;
        }
        public ModelAttribute GetModelAttribute<T>()
        {
            return GetModelAttribute(typeof(T));
        }
        private static ModelAttribute GetModelAttribute(Type type)
        {
            ModelAttribute m = type.GetCustomAttribute<ModelAttribute>();
            if (m == null)
            {
                throw new Exception("模型类" + type.Name + "未使用ModelAttribute，[Model(TableName = \"t_article\", PrimaryKey = \"id\", IdentityInsert = true)]");
            }
            else
            {
                return m;
            }
        }
        public PropertyInfo[] GetStorageablePropertys<T>()
        {
            return GetStorageablePropertys(typeof(T));
        }
        private static PropertyInfo[] GetStorageablePropertys(Type type)
        {
            PropertyInfo[] properties = type.GetProperties();// 获得此模型的公共属性
            List<PropertyInfo> result = new List<PropertyInfo>();
            foreach (PropertyInfo pi in properties)
            {
                FieldAttribute fa = pi.GetCustomAttribute<FieldAttribute>();
                if (fa == null || fa.Storageable) result.Add(pi);
            }
            return result.ToArray();
        }
        public PropertyInfo[] GetWebablePropertys<T>(int group_code = 0)
        {
            return GetWebablePropertys(typeof(T), group_code);
        }
        public static PropertyInfo[] GetWebablePropertys(Type type, int group_code = 0)
        {
            PropertyInfo[] properties = type.GetProperties();// 获得此模型的公共属性
            List<PropertyInfo> result = new List<PropertyInfo>();
            foreach (PropertyInfo pi in properties)
            {
                FieldAttribute fa = pi.GetCustomAttribute<FieldAttribute>();
                if (fa == null) result.Add(pi);
                else if (fa.Webable && (group_code == 0 || fa.Groups.Contains<int>(group_code))) result.Add(pi);
            }
            return result.ToArray();
        }
        #endregion

        #region 转JSON
        public static string ObjToJson(object obj, int group_code = 0)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings();
            setting.Converters.Add(new ModelConvert(group_code));
            return JsonConvert.SerializeObject(obj, Formatting.Indented, setting);
        }
        public string ToJson(object obj, int group_code = 0)
        {
            return ObjToJson(obj, group_code);
        }
        #endregion

        #region 数据库创建函数
        public void CreateDataBase(List<Type> table_list, Dictionary<string, string> stored_precedures = null)
        {
            // 创建数据库
            if (TestDatabaseExists())
            {
                // 创建表
                foreach (Type t in table_list)
                {
                    if (TestTableExists(t)) CheckFields(t);
                    else
                    {
                        CreateTable(t);
                        AddInitData(t);
                    }
                }
                // 创建存储过程
                if (stored_precedures != null) {
                    foreach(string key in stored_precedures.Keys)
                    {
                        if (TestStoredProcedureExists(key)) DeleteStoredProcedure(key);
                        AddStoredProcedure(stored_precedures[key]);
                    }
                }
                // 创建视图，约束等等，暂时可能先不考虑这些
            }else
            {
                // 不存在数据库，则不需要做任何判断，直接创建即可
                _CreateDataBase();
                // 创建表
                foreach (Type t in table_list)
                {
                    CreateTable(t);
                    AddInitData(t);
                }
                // 创建存储过程
                if (stored_precedures != null)
                {
                    foreach (string key in stored_precedures.Keys)
                    {
                        AddStoredProcedure(stored_precedures[key]);
                    }
                }
                // 创建视图，约束等等，暂时可能先不考虑这些
            }
        }
        private void _CreateDataBase()
        {
            SqlHelper MDB = new SqlHelper(this.ServerName, "master", this.UserName, this.Password);
            MDB.Set(string.Format("create database {0}", this.DatabaseName));
        }
        private void CreateTable(Type model_type)
        {
            ModelAttribute ma = GetModelAttribute(model_type);
            string columns_str = "";
            PropertyInfo[] pis = GetStorageablePropertys(model_type);
            for (int i = 0; i < pis.Length; i++)
            {
                PropertyInfo pi = pis[i];
                FieldAttribute fa = pi.GetCustomAttribute<FieldAttribute>();
                FieldAttribute temp_fa = new FieldAttribute();
                bool is_primary_key = false;

                if (fa != null && fa.DataType != null) temp_fa.DataType = fa.DataType;
                else temp_fa.DataType = GetDataType(pi.PropertyType);

                if (ma.PrimaryKey == pi.Name) is_primary_key = true;

                if (is_primary_key) temp_fa.Nullable = false;
                else if (fa != null) temp_fa.Nullable = fa.Nullable;

                columns_str += string.Format("{0} {1} {2} {3}", pi.Name, temp_fa.DataType, is_primary_key? (ma.IdentityInsert? "identity(1,1)" : "")+" PRIMARY KEY" : "", temp_fa.Nullable? "" : "NOT NULL");
                if (i != pis.Length - 1) columns_str += ",";
            }
            string table_str = string.Format("create table {0} ({1})", ma.TableName, columns_str);
            Set(table_str);
        }
        private void CheckFields(Type model_type)
        {
            ModelAttribute ma = GetModelAttribute(model_type);
            PropertyInfo[] pis = GetStorageablePropertys(model_type);

            foreach (PropertyInfo pi in pis)
            {
                FieldAttribute fa = pi.GetCustomAttribute<FieldAttribute>();
                FieldAttribute temp_fa = new FieldAttribute();
                bool is_primary_key = false;

                if (fa != null && fa.DataType != null) temp_fa.DataType = fa.DataType;
                else temp_fa.DataType = GetDataType(pi.PropertyType);

                if (ma.PrimaryKey == pi.Name) is_primary_key = true;

                if (is_primary_key) temp_fa.Nullable = false;
                else if (fa != null) temp_fa.Nullable = fa.Nullable;

                if (TestFieldExists(ma.TableName, pi.Name)) CheckFieldDataType(ma.TableName, pi.Name, temp_fa.DataType, is_primary_key, ma.IdentityInsert, temp_fa.Nullable);
                else AddField(ma.TableName, pi.Name, temp_fa.DataType, is_primary_key, ma.IdentityInsert, temp_fa.Nullable);
            }
        }
        private void AddField(string table_name, string field_name, string data_type, bool is_primary_key, bool is_identity_insert, bool null_able)
        {
            string field_str = string.Format("alter table {0} add {1} {2} {3} {4}", table_name, field_name, data_type, is_primary_key ? (is_identity_insert ? "identity(1,1)" : "") + " PRIMARY KEY" : "", null_able ? "" : "NOT NULL");
            Set(field_str);
        }
        private string GetDataType(Type type)
        {
            if (type == typeof(int)) return "int";
            else if (type == typeof(string)) return "nvarchar(50)";
            return "nvarchar(50)";
        }
        public bool TestDatabaseExists()
        {
            string test_str = string.Format("if exists(select * from sys.databases where name = '{0}') begin select 1 end else begin select 0 end", this.DatabaseName);
            return (int)Get(test_str).ScalarValue == 1;
        }
        private bool TestTableExists(Type type)
        {
            string test_str = string.Format("if exists (select * from sysobjects where id = object_id(N'{0}') and OBJECTPROPERTY(id, N'IsUserTable') = 1) begin select 1 end else begin select 0 end", GetTableName(type));
            return (int)Get(test_str).ScalarValue == 1;
        }
        private bool TestFieldExists(string table_name, string field_name)
        {
            string test_str = string.Format("if exists(select * from syscolumns where id=object_id('{0}') and name='{1}') begin select 1 end else begin select 0 end", table_name, field_name);
            return (int)Get(test_str).ScalarValue == 1;
        }
        private bool TestStoredProcedureExists(string name)
        {
            string test_str = string.Format("if exists(select * from sysobjects where id = object_id(N'{0}') and OBJECTPROPERTY(id, N'IsProcedure') = 1) begin select 1 end else begin select 0 end", name);
            return (int)Get(test_str).ScalarValue == 1;
        }
        private void CheckFieldDataType(string table_name, string field_name, string data_type, bool is_primary_key, bool is_identity_insert, bool null_able)
        {
            // 如果字段类型不一致，更改
            // 如果当前字段为主键，则需要检测其他字段是否为主键，将其改为非主键
        }
        private void AddInitData(Type type)
        {
            MethodInfo mi = type.GetMethod("GetInitData");
            if (mi == null) return;
            object[] result = (object[])mi.Invoke(null, null);
            foreach(object o in result)
            {
                Insert(type, o);
            }
        }
        private void DeleteStoredProcedure(string name)
        {
            string test_str = string.Format("drop procedure {0}", name);
            Set(test_str);
        }
        private void AddStoredProcedure(string file_path)
        {
            System.Diagnostics.Process sqlProcess = new System.Diagnostics.Process();
            sqlProcess.StartInfo.FileName = "osql.exe ";
            sqlProcess.StartInfo.Arguments = string.Format("-S {0} -U {1} -P {2} -d {3} -i {4}", ServerName, UserName, Password, DatabaseName, file_path);
            sqlProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            sqlProcess.Start();
            sqlProcess.WaitForExit();//程序安装过程中执行
            sqlProcess.Close();
        }
        public Dictionary<string, string> GetSqlFiles(string folder_path, string[] patterns = null)
        {
            if (patterns == null) patterns = new string[] { "*.txt", "*.sql"};
            Dictionary<string, string> result = new Dictionary<string, string>();
            DirectoryInfo TheFolder = new DirectoryInfo(folder_path);

            foreach (DirectoryInfo NextFolder in TheFolder.GetDirectories())
            {
                Dictionary<string, string> temp = GetSqlFiles(NextFolder.FullName, patterns);
                foreach(string key in temp.Keys)
                {
                    result.Add(key, temp[key]);
                }
            }

            foreach(string pattern in patterns)
            {
                foreach (FileInfo NextFile in TheFolder.GetFiles(pattern))
                {
                    //StreamReader sr = new System.IO.StreamReader(NextFile.FullName);
                    //string line_str;
                    //string content_str = "";
                    //while ((line_str = sr.ReadLine()) != null)
                    //{
                    //    content_str += line_str + " ";
                    //}
                    result.Add(NextFile.Name.Split('.')[0], NextFile.FullName);
                }
            }
            return result;
        }
        #endregion
    }

    #region Attribute类,JsonConverter类
    [AttributeUsage(AttributeTargets.Class)]
    public class ModelAttribute : Attribute
    {
        public string TableName { get; set; } // 表名
        public string PrimaryKey { get; set; } // 主键
        public bool IdentityInsert { get; set; } // 识别插入，即主键自增

        public ModelAttribute()
        {
            PrimaryKey = "id";
            IdentityInsert = true;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FieldAttribute : Attribute
    {
        public bool Storageable { get; set; } // 是否是数据库字段,是否可存储
        public bool Webable { get; set; } // 是否可以被渲染到前端
        public bool Nullable { get; set; } // 是否允许为null
        public string DataType { get; set; } // 数据类型
        public int[] Groups { get; set; }

        public FieldAttribute()
        {
            Storageable = true;
            Webable = true;
            Nullable = true;
            Groups = new int[] { };
        }
    }

    class ModelConvert : JsonConverter
    {
        private int group_code = 0;
        private Type current_type = null;

        public ModelConvert(int the_group_code = 0)
        {
            group_code = the_group_code;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return existingValue;
        }
        
        public override bool CanConvert(Type objectType)
        {
            bool b = objectType.GetCustomAttribute<ModelAttribute>() != null;
            if(b) current_type = objectType;
            return b;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            // 循环属性，每个属性再序列化，输出
            PropertyInfo[] pis = SqlHelper.GetWebablePropertys(current_type, group_code);

            writer.WriteStartObject();
            foreach(PropertyInfo pi in pis)
            {
                writer.WritePropertyName(pi.Name);
                writer.WriteRawValue(SqlHelper.ObjToJson(pi.GetValue(value), group_code));
            }
            writer.WriteEndObject();
        }
    }
    #endregion
}