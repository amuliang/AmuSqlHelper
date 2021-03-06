﻿using System;
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
        private static Dictionary<Type, ParsedModel> ParsedModels = new Dictionary<Type, ParsedModel>();
        // 获得解析后的Model数据
        public static ParsedModel GetParsedModel(Type type)
        {
            if(!ParsedModels.ContainsKey(type))
            {
                ParsedModel temp = new ParsedModel();
                ModelAttribute m = type.GetCustomAttribute<ModelAttribute>();
                if (m == null)
                {
                    throw new Exception(string.Format("模型类{0}未使用ModelAttribute，[Model(TableName = \"t_article\", PrimaryKey = \"id\", IdentityInsert = true)]", type.Name));
                }
                else
                {
                    temp.Model = m;
                }

                temp.WebableFields = new Dictionary<string, FieldAttribute>();
                temp.StorageableFields = new Dictionary<string, FieldAttribute>();

                PropertyInfo[] properties = type.GetProperties().Where(r => r.GetMethod != null && r.GetMethod.IsStatic == false).ToArray(); // 获得此模型的公共属性
                foreach (PropertyInfo pi in properties)
                {
                    FieldAttribute fa = pi.GetCustomAttribute<FieldAttribute>();
                    if(fa == null)
                    {
                        fa = new FieldAttribute();
                    }
                    if (fa.FieldName == null || fa.FieldName == "") fa.FieldName = pi.Name; // 检测是否指定了数据库表名
                    if(fa.FieldName == m.PrimaryKey) // 检测是否是主键
                    {
                        fa.SetIsPrimaryKey(true);
                        fa.SetIdentityInsert(m.IdentityInsert);
                    }
                    if (fa.IsPrimaryKey) // 如果是主键则不能为空
                    {
                        fa.Nullable = false;
                    }
                    else if (fa.Nullable == false && fa.Default == null) // 如果可为空，则必须指定默认值
                    {
                        throw new Exception(string.Format("模型类{0}的字段{1}不可为空，需要提供Default默认值", m.TableName, pi.Name));
                    }
                    fa.SetPropertyInfo(pi);
                    if (fa.DataType == null) fa.DataType = GetDataType(fa.PropertyInfo.PropertyType); // 如果未指定数据类型，则指定默认类型

                    if (fa.Storageable && fa.PropertyInfo.CanRead && fa.PropertyInfo.CanWrite) temp.StorageableFields.Add(pi.Name, fa);
                    if (fa.Webable && fa.PropertyInfo.CanRead) temp.WebableFields.Add(pi.Name, fa);

                }
                ParsedModels.Add(type, temp);
            }
            return ParsedModels[type];
        }

        #region 条件list数据
        public SqlResult GetPage(string table, string condition, string order_by, int skip, int take)
        {
            SqlParameter[] param = new SqlParameter[]
            {
                CreateSqlParameter("@table_name", SqlDbType.NVarChar, table),
                CreateSqlParameter("@condition", SqlDbType.NVarChar, condition),
                CreateSqlParameter("@order_by", SqlDbType.NVarChar, order_by),
                CreateSqlParameter("@skip", SqlDbType.Int, skip),
                CreateSqlParameter("@take", SqlDbType.Int, take)
            };
            return Get("sp_amu_getPageData", CommandType.StoredProcedure, param);
        }
        public SqlResult<T> GetPage<T>(string condition, string order_by, int skip, int take) where T : class, new()
        {
            SqlParameter[] param = new SqlParameter[]
            {
                CreateSqlParameter("@table_name", SqlDbType.NVarChar, GetTableName<T>()),
                CreateSqlParameter("@condition", SqlDbType.NVarChar, condition),
                CreateSqlParameter("@order_by", SqlDbType.NVarChar, order_by),
                CreateSqlParameter("@skip", SqlDbType.Int, skip),
                CreateSqlParameter("@take", SqlDbType.Int, take)
            };
            return HGet<T>("sp_amu_getPageData", CommandType.StoredProcedure, param);
        }
        public SqlResult<T> Get<T>(string condition, string order_by = "", int size = 1000) where T : class, new()
        {
            string order_by_str = order_by == "" ? "" : string.Format("order by {0}", order_by);
            string condition_str = condition == "" ? "" : string.Format("where {0}", condition);
            string top_str = size <= 0 ? "" : string.Format("top {0}", size);
            string sql_str = string.Format("select {0} * from [{1}] {2} {3}", top_str, GetTableName<T>(), condition_str, order_by_str);
            return HGet<T>(sql_str, CommandType.Text);
        }
        public SqlResult<T> Get<T>(int size = 1000) where T : class, new()
        {
            return Get<T>("", "", size);
        }
        #endregion

        #region 特殊函数
        public int GetCount<T>(string condition = "")
        {
            string sql_str = string.Format("select count(*) from {0}", GetTableName<T>());
            if (condition != null && condition != "")
            {
                sql_str += " where " + condition;
            }
            return (int)Get(sql_str).ScalarValue;
        }
        public int GetCount(string table, string condition = "")
        {
            string sql_str = string.Format("select count(*) from {0}", table);
            if (condition != null && condition != "")
            {
                sql_str += " where " + condition;
            }
            return (int)Get(sql_str).ScalarValue;
        }
        private object GetMax(Type type, string prop_name, object default_value = null)
        {
            string sql_str = string.Format("select max({0}) from {1}", prop_name, GetTableName(type));
            object result = Get(sql_str).ScalarValue;
            if(result == null)
            {
                return default_value;
            }
            else
            {
                if (int.Parse(result.ToString()) < int.Parse(default_value.ToString()))
                {
                    return default_value;
                }
                else return result;
            }
        }
        public object GetMax<T>(string prop_name, object default_value = null)
        {
            return GetMax(typeof(T), prop_name, default_value);
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
            ModelAttribute ma = GetParsedModel(typeof(T)).Model;
            string sql_str = string.Format("select * from [{0}] where [{1}]='{2}'", ma.TableName, ma.PrimaryKey, id);
            return Get(sql_str).GetFirstEntity<T>();
        }
        public T GetById<T>(int id) where T : class, new()
        {
            return GetById<T>(id.ToString());
        }
        
        private int Insert(Type type, object obj)
        {
            ParsedModel pm = GetParsedModel(type);
            Dictionary<string, FieldAttribute> fas = pm.StorageableFields;
            string columns = "";
            string values = "";
            int len = fas.Count;
            FieldAttribute fa;
            PropertyInfo pi;
            bool notLast;
            int i = 0;
            foreach(string key in fas.Keys)
            {
                i++;
                fa = fas[key];
                pi = fa.PropertyInfo;
                if (fa.IsPrimaryKey)
                {
                    if (fa.IdentityInsert) continue;
                    else
                    {
                        object temp = pi.GetValue(obj);
                        if (temp == null || temp.ToString() == "" || (int)temp == 0)
                        {
                            int new_id = int.Parse(GetMax(type, fa.FieldName, pm.Model.BaseID).ToString()) + 1;
                            pi.SetValue(obj, Convert.ChangeType(new_id, pi.PropertyType));
                        }
                    }
                } 
                notLast = i < len;
                columns += "[" + fa.FieldName + "]" + (notLast ? "," : "");
                // 非空字段，检测是否为null，是null则应用默认值
                object temp_value = pi.GetValue(obj);
                if (temp_value == null) temp_value = fa.Default;
                values += "'" + temp_value + "'" + (notLast ? "," : "");
            }
            string sql_str = string.Format("insert into [{0}] ({1}) values ({2});select @@IDENTITY", pm.Model.TableName, columns, values);
            SqlResult sr = Get(sql_str);
            if (pm.Model.IdentityInsert)
            {
                if ((pi = type.GetProperty(pm.Model.PrimaryKey)) != null)
                {
                    pi.SetValue(obj, Convert.ChangeType(sr.ScalarValue, pi.PropertyType));
                }
            }
            return 1;
        }
        public int Insert<T>(T obj) where T : class, new()
        {
            return Insert(typeof(T), obj);
        }
        public int Insert<T>(List<T> obj_list) where T : class, new()
        {
            SqlHelper sh = new SqlHelper(ConnectionString);
            sh.OpenStack();
            for(int i = 0; i < obj_list.Count; i++)
            {
                Insert<T>(obj_list[i]);
            }
            return sh.RunStack().EffectedLineCount;
        }
        
        public int Update<T>(T obj) where T : class, new()
        {
            ParsedModel pm = GetParsedModel(typeof(T));
            PropertyInfo pi;
            FieldAttribute fa;
            string id = "";
            if ((pi = typeof(T).GetProperty(pm.Model.PrimaryKey)) != null)
            {
                id = pi.GetValue(obj).ToString();
            }
            if (id == "") throw new Exception(string.Format("更新表数据时，未向实例提供主键值，表：{0}，主键：{1}", typeof(T).Name, pm.Model.PrimaryKey));

            Dictionary<string, FieldAttribute> fas = pm.StorageableFields;
            string keyvalues = "";
            int len = fas.Count;
            bool notLast;
            int i = 0;
            foreach(string key in fas.Keys)
            {
                i++;
                fa = fas[key];
                pi = fa.PropertyInfo;
                if (fa.IsPrimaryKey) continue;
                notLast = i < len;
                // 非空字段，检测是否为null，是null则应用默认值
                object temp_value = pi.GetValue(obj);
                if (temp_value == null) temp_value = fa.Default;
                keyvalues += string.Format("[{0}]='{1}'", fa.FieldName, temp_value) + (notLast ? "," : "");
            }
            string sql_str = string.Format("update [{0}] set {1} where [{2}]='{3}'", pm.Model.TableName, keyvalues, pm.Model.PrimaryKey, id);
            return Set(sql_str).EffectedLineCount;
        }
        
        public int Delete<T>(int id)
        {
            return Delete<T>(id.ToString());
        }
        public int Delete<T>(string id)
        {
            ModelAttribute ma = GetParsedModel(typeof(T)).Model;
            string sql_str = string.Format("delete from [{0}] where [{1}]='{2}'", ma.TableName, ma.PrimaryKey, id);
            return Set(sql_str).EffectedLineCount;
        }
        public int DeleteByCondition<T>(string condition)
        {
            ModelAttribute ma = GetParsedModel(typeof(T)).Model;
            string sql_str = string.Format("delete from [{0}] where {1}", ma.TableName, condition);
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
            return GetParsedModel(type).Model.TableName;
        }
        public string GetPrimaryKey<T>()
        {
            return GetParsedModel(typeof(T)).Model.PrimaryKey;
        }
        public bool GetIdentityInsert<T>()
        {
            return GetParsedModel(typeof(T)).Model.IdentityInsert;
        }
        public PropertyInfo[] GetWebablePropertys<T>(int group_code = 0)
        {
            return GetWebablePropertys(typeof(T), group_code);
        }
        public static PropertyInfo[] GetWebablePropertys(Type type, int group_code = 0)
        {
            Dictionary<string, FieldAttribute> fas = GetParsedModel(type).WebableFields;
            List<PropertyInfo> result = new List<PropertyInfo>();
            foreach (string key in fas.Keys)
            {
                FieldAttribute fa = fas[key];
                if(group_code == 0 || fa.Groups.Contains<int>(group_code)) result.Add(fa.PropertyInfo);
            }
            return result.ToArray();
        }
        #endregion

        #region 转JSON
        public static string ObjToJson(object obj, int group_code = 0, bool show_private = true)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings();
            setting.Converters.Add(new ModelConvert(group_code, show_private));
            return JsonConvert.SerializeObject(obj, Formatting.Indented, setting);
        }
        public static string ObjToJson(object obj, string columns, bool show_private = true)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings();
            setting.Converters.Add(new ModelConvert(columns, show_private));
            return JsonConvert.SerializeObject(obj, Formatting.Indented, setting);
        }
        public string ToJson(object obj, int group_code = 0, bool show_private = true)
        {
            return ObjToJson(obj, group_code, show_private);
        }
        public string ToJson(object obj, string columns, bool show_private = true)
        {
            return ObjToJson(obj, columns, show_private);
        }
        #endregion

        #region 数据库创建函数
        private List<string> DataBaseUpdateLogs = null;
        public List<string> CreateDataBase(List<Type> table_list, Dictionary<string, string> stored_precedures = null)
        {
            DataBaseUpdateLogs = new List<string>();
            DateTime start_time = DateTime.Now;
            DataBaseUpdateLogs.Add(string.Format("开始更新数据库，开始时间{0}", start_time));
            // 创建数据库
            if (TestDatabaseExists())
            {
                DataBaseUpdateLogs.Add(string.Format("数据库{0}已存在，正在对比表", this.DatabaseName));
                // 创建表
                foreach (Type t in table_list)
                {
                    if (TestTableExists(t))
                    {
                        CheckFields(t);
                    }
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
                        if (!TestStoredProcedureExists(key)) AddStoredProcedure(stored_precedures[key]);
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
            DateTime end_time = DateTime.Now;
            DataBaseUpdateLogs.Add(string.Format("数据库更新完成，完成时间{0}，共耗时{1}", end_time, end_time - start_time));
            List<string> temp = DataBaseUpdateLogs;
            DataBaseUpdateLogs = null;
            return temp;
        }
        private void _CreateDataBase()
        {
            SqlHelper MDB = new SqlHelper(this.ServerName, "master", this.UserName, this.Password);
            MDB.Set(string.Format("create database {0}", this.DatabaseName));
            DataBaseUpdateLogs.Add("创建数据库" + this.DatabaseName);
        }
        private void CreateTable(Type model_type)
        {
            ParsedModel pm = GetParsedModel(model_type);
            ModelAttribute ma = GetParsedModel(model_type).Model;
            Dictionary<string, FieldAttribute> fas = pm.StorageableFields;
            string columns_str = "";
            int len = fas.Count;
            int i = 0;
            foreach (string key in fas.Keys)
            {
                i++;
                FieldAttribute fa = fas[key];
                columns_str += string.Format("{0} {1} {2} {3}", fa.FieldName, fa.DataType, fa.IsPrimaryKey? (ma.IdentityInsert? "identity(1,1)" : "")+" PRIMARY KEY" : "", fa.Nullable? "" : "NOT NULL");
                if (i != len) columns_str += ",";
            }
            string table_str = string.Format("create table {0} ({1})", ma.TableName, columns_str);
            Set(table_str);
            DataBaseUpdateLogs.Add("创建表" + ma.TableName);
        }
        private void CheckFields(Type model_type)
        {
            ParsedModel pm = GetParsedModel(model_type);
            ModelAttribute ma = GetParsedModel(model_type).Model;
            Dictionary<string, FieldAttribute> fas = pm.StorageableFields;
            DataBaseUpdateLogs.Add(string.Format("表{0}已存在，正在对比字段", ma.TableName));

            foreach (string key in fas.Keys)
            {
                FieldAttribute fa = fas[key];
                if (TestFieldExists(ma.TableName, fa.FieldName)) CheckFieldDataType(ma.TableName, fa.FieldName, fa.DataType, fa.IsPrimaryKey, ma.IdentityInsert, fa.Nullable);
                else AddField(ma.TableName, fa.FieldName, fa.DataType, fa.IsPrimaryKey, ma.IdentityInsert, fa.Nullable);
            }
        }
        private void AddField(string table_name, string field_name, string data_type, bool is_primary_key, bool is_identity_insert, bool null_able)
        {
            string field_str = string.Format("alter table {0} add {1} {2} {3} {4}", table_name, field_name, data_type, is_primary_key ? (is_identity_insert ? "identity(1,1)" : "") + " PRIMARY KEY" : "", null_able ? "" : "NOT NULL");
            Set(field_str);
            DataBaseUpdateLogs.Add(string.Format("为表{0}添加字段{1}", table_name, field_name));
        }
        private static string GetDataType(Type type)
        {
            if (type == typeof(int)) return "int";
            else if (type == typeof(string)) return "nvarchar(50)";
            else if (type == typeof(double)) return "numeric(18,0)";
            else if (type == typeof(DateTime)) return "datetime";
            return "nvarchar(50)";
        }
        public bool TestDatabaseExists()
        {
            SqlHelper MDB = new SqlHelper(this.ServerName, "master", this.UserName, this.Password);
            string test_str = string.Format("if exists(select * from sys.databases where name = '{0}') begin select 1 end else begin select 0 end", this.DatabaseName);
            return (int)MDB.Get(test_str).ScalarValue == 1;
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
            string test_str = string.Format("select c.name, b.length from sysobjects a, syscolumns b ,systypes c where a.id = b.id and a.xtype='U' and b.xtype=c.xtype and a.name='{0}' and b.name='{1}'",
                table_name, field_name);
            SqlResult sr = Get(test_str);
            //string old_type = sr.FirstTable.Rows[0]["name"].ToString();
            //string old_length = sr.FirstTable.Rows[0]["length"].ToString();
            //if (old_type != data_type && string.Format("{0}({1})", old_type, old_length) != data_type)
            //{
            //    DataBaseUpdateLogs.Add(string.Format("表{0}字段{1}类型冲突：{2}({3}) => {4}", table_name, field_name, old_type, old_length, data_type));
            //}
            string old_type = sr.FirstTable.Rows[0]["name"].ToString();
            string now_type = data_type.Split('(')[0];
            if (old_type != now_type)
            {
                DataBaseUpdateLogs.Add(string.Format("表{0}字段{1}类型冲突：{2} => {3}", table_name, field_name, old_type, now_type));
            }
            // 如果当前字段为主键，则需要检测其他字段是否为主键，将其改为非主键
            //DataBaseUpdateLogs.Add(string.Format("表{0}字段{1}已存在，未做处理", table_name, field_name));

            //select a.name 表名, b.name 字段名, c.name 字段类型, b.length 字段长度
            //from sysobjects a, syscolumns b, systypes c
            //where a.id = b.id and a.xtype = 'U' and b.xtype = c.xtype
            //and a.name = 't_article' and b.name = 'id'
        }
        private void AddInitData(Type type)
        {
            MethodInfo mi = type.GetMethod("GetInitData");
            if (mi == null) return;
            object[] result = (object[])mi.Invoke(null, null);
            DataBaseUpdateLogs.Add(string.Format("正在为表{0}插入{1}条数据", GetParsedModel(type).Model.TableName, result.Length));
            foreach (object o in result)
            {
                Insert(type, o);
            }
            DataBaseUpdateLogs.Add(string.Format("为表{0}成功插入{1}条数据", GetParsedModel(type).Model.TableName, result.Length));
        }
        private void DeleteStoredProcedure(string name)
        {
            string test_str = string.Format("drop procedure {0}", name);
            Set(test_str);
            DataBaseUpdateLogs.Add(string.Format("存储过程{0}已删除", name));
        }
        private void AddStoredProcedure(string file_path)
        {
            System.Diagnostics.Process sqlProcess = new System.Diagnostics.Process();
            sqlProcess.StartInfo.FileName = "osql.exe ";
            sqlProcess.StartInfo.Arguments = string.Format("-S {0} -U {1} -P {2} -d {3} -i \"{4}\"", ServerName, UserName, Password, DatabaseName, file_path);
            sqlProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            sqlProcess.Start();
            sqlProcess.WaitForExit();//程序安装过程中执行
            sqlProcess.Close();
            DataBaseUpdateLogs.Add(string.Format("执行文件{0}，创建存储过程", file_path));
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

    #region Attribute类,JsonConverter类, 其他辅助类
    [AttributeUsage(AttributeTargets.Class)]
    public class ModelAttribute : Attribute
    {
        public string TableName { get; set; } // 表名
        public string PrimaryKey { get; set; } // 主键
        public bool IdentityInsert { get; set; } // 识别插入，即主键自增
        public string BaseID { get; set; } // 当IdentityInsert为false时，数据从这里递增
        public string Description { get; set; }

        public ModelAttribute()
        {
            PrimaryKey = "id";
            IdentityInsert = true;
            BaseID = "0";
            Description = "";
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FieldAttribute : Attribute
    {
        public string FieldName { get; set; } // 字段名称
        public string Description { get; set; }
        public bool Storageable { get; set; } // 是否是数据库字段,是否可存储
        public bool Webable { get; set; } // 是否可以被渲染到前端
        public bool Nullable { get; set; } // 是否允许为null
        public string DataType { get; set; } // 数据类型
        public object Default { get; set; } // 默认值
        public bool IsPrivate { get; set; } // 是否私有，作为其它类的属性看不到的字段
        private PropertyInfo pi { get; set; }
        public PropertyInfo PropertyInfo
        {
            get
            {
                return pi;
            }
        }
        private bool is_pk { get; set; }
        public bool IsPrimaryKey
        {
            get
            {
                return is_pk;
            }
        }
        private bool ii { get; set; }
        public bool IdentityInsert
        {
            get
            {
                return ii;
            }
        }
        public int[] Groups { get; set; }

        public FieldAttribute()
        {
            Description = "";
            Storageable = true;
            Webable = true;
            Nullable = true;
            IsPrivate = false;
            DataType = null;
            Groups = new int[] { 0 };
            is_pk = false;
            ii = false;
        }

        public void SetPropertyInfo(PropertyInfo pi)
        {
            this.pi = pi;
        }
        public void SetIsPrimaryKey(bool b)
        {
            is_pk = b;
        }
        public void SetIdentityInsert(bool b)
        {
            ii = b;
        }
    }

    public class ParsedModel
    {
        public ModelAttribute Model { get; set; }
        public Dictionary<string, FieldAttribute> WebableFields { get; set; }
        public Dictionary<string, FieldAttribute> StorageableFields { get; set; }
    }

    class ModelConvert : JsonConverter
    {
        private int group_code = 0;
        private string columns = "";
        private Type current_type = null;
        private bool show_private = true;

        public ModelConvert(int the_group_code = 0, bool the_show_private = true)
        {
            group_code = the_group_code;
            show_private = the_show_private;
        }

        public ModelConvert(string the_columns, bool the_show_private = true)
        {
            columns = the_columns;
            show_private = the_show_private;
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
            Dictionary<string, FieldAttribute> was = SqlHelper.GetParsedModel(current_type).WebableFields;
            string[] the_columns = columns.Split(',');

            writer.WriteStartObject();
            foreach(string key in was.Keys)
            {
                FieldAttribute wa = was[key];
                if (!show_private && wa.IsPrivate) continue; // 当不显示私有字段，并且当前字段正好为私有的，则跳过（此私有非类的公有私有）
                if(group_code == 0 && columns != "")
                {
                    if (!the_columns.Contains<string>(key)) continue;
                }else
                {
                    if (!wa.Groups.Contains<int>(group_code)) continue;
                }
                PropertyInfo pi = wa.PropertyInfo;
                writer.WritePropertyName(pi.Name);
                writer.WriteRawValue(SqlHelper.ObjToJson(pi.GetValue(value), 0, false));
            }
            writer.WriteEndObject();
        }
    }

    // 数据库condition拼接类
    public class Cd
    {
        public List<string> list = new List<string>();

        public Cd(string condition = null, params object[] values)
        {
            if (condition != null) this.And(condition, values);
        }
        public Cd And(string condition, params object[] values)
        {
            if (condition == null || condition.Trim() == "") return this;
            condition = string.Format(condition, values);
            if (list.Count == 0)
            {
                list.Add(string.Format("({0})", condition));
            }
            else
            {
                list.Add(string.Format(" and ({0})", condition));
            }
            return this;
        }
        public Cd And(Cd dbop)
        {
            return And(dbop.Render());
        }
        public Cd Or(string condition, params object[] values)
        {
            if (condition == null || condition.Trim() == "") return this;
            condition = string.Format(condition, values);
            if (list.Count == 0)
            {
                list.Add(condition);
            }
            else
            {
                list.Add(string.Format(" or ({0})", condition));
            }
            return this;
        }
        public Cd Or(Cd dbop)
        {
            return Or(dbop.Render());
        }
        public string Render()
        {
            return list.Count == 0 ? "(1=1)" : string.Format("({0})", string.Join("", list));
        }
    }
    // 数据库orderby拼接类
    public class Ob
    {
        public Dictionary<string, string> list = new Dictionary<string, string>();
        public int Count {
            get
            {
                return list.Count;
            }
        }
        public Ob(string field_name = null)
        {
            if (field_name != null) this.Desc(field_name);
        }
        public Ob Asc(string field_name)
        {
            if (field_name == null || field_name.Trim() == "") return this;

            list[field_name] = "asc";

            return this;
        }
        public Ob Desc(string field_name)
        {
            if (field_name == null || field_name.Trim() == "") return this;

            list[field_name] = "desc";

            return this;
        }
        //public Ob Add(string field_name)
        //{
        //    if (field_name == null || field_name.Trim() == "") return this;

        //    list[field_name] = "";

        //    return this;
        //}
        //public Ob Add(Ob ob)
        //{
        //    return Add(ob.Render());
        //}
        public string Render()
        {
            return string.Join(",", list.Select(r => r.Key + " " + r.Value));
        }
    }
    #endregion
}