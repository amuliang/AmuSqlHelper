using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using Newtonsoft.Json;

namespace AmuTools
{
    /// <summary>
    /// SqlHelperEx 的摘要说明
    /// </summary>
    public class SqlHelperEx
    {
        #region 条件list数据
        public static SqlResult GetPage<T>(string condition, string order_by, int skip, int take)
        {
            SqlParameter[] param = new SqlParameter[]
            {
            SqlHelper.CreateSqlParameter("@table_name", SqlDbType.NVarChar, GetTableName<T>()),
            SqlHelper.CreateSqlParameter("@condition", SqlDbType.NVarChar, condition),
            SqlHelper.CreateSqlParameter("@order_by", SqlDbType.NVarChar, order_by),
            SqlHelper.CreateSqlParameter("@skip", SqlDbType.Int, skip),
            SqlHelper.CreateSqlParameter("@take", SqlDbType.Int, take)
            };
            return SqlHelper.Get("sp_amu_getPageData", CommandType.StoredProcedure, param);
        }
        public static SqlResult Get<T>(string condition = "", string order_by = "", int size = 1000) where T : class, new()
        {
            string order_by_str = order_by == "" ? "" : string.Format("orderby {0}", order_by);
            string condition_str = condition == "" ? "" : string.Format("where {0}", condition);
            string sql_str = string.Format("select top {0} * from [{1}] {2} {3}", size, GetTableName<T>(), condition_str, order_by_str);
            return SqlHelper.Get(sql_str);
        }
        #endregion

        #region 特殊函数
        public static int GetCount<T>(string condition)
        {
            string sql_str = string.Format("select count(*) from {0}", GetTableName<T>());
            if (condition != null && condition != "")
            {
                sql_str += " where " + condition;
            }
            return (int)SqlHelper.Get(sql_str).ScalarValue;
        }
        public static object GetMax<T>(string prop_name)
        {
            string sql_str = string.Format("select max({0}) from {1}", prop_name, GetTableName<T>());
            return SqlHelper.Get(sql_str).ScalarValue == null ? 0 : SqlHelper.Get(sql_str).ScalarValue;
        }
        public static bool IsOne<T>(string condition)
        {
            string where_str = condition == null || condition == "" ? "" : string.Format(" where {0}", condition);
            string sql_str = string.Format("select count(*) from {0} {1}", GetTableName<T>(), where_str);
            return (int)SqlHelper.Get(sql_str).ScalarValue == 1;
        }
        #endregion

        #region 增删改查
        public static T GetById<T>(string id) where T : class, new()
        {
            ModelAttribute ma = GetModelAttribute<T>();
            string sql_str = string.Format("select * from [{0}] where [{1}]='{2}'", ma.TableName, ma.PrimaryKey, id);
            return SqlHelper.Get(sql_str).GetFirstEntity<T>();
        }
        public static T GetById<T>(int id) where T : class, new()
        {
            return GetById<T>(id.ToString());
        }
        
        public static int Insert<T>(T obj) where T : class, new()
        {
            ModelAttribute ma = GetModelAttribute<T>();
            PropertyInfo[] properties = GetStorageablePropertys<T>();// 获得此模型的公共属性
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
            SqlResult sr = SqlHelper.Get(sql_str);
            if (ma.IdentityInsert == true)
            {
                if ((pi = typeof(T).GetProperty(ma.PrimaryKey)) != null)
                {
                    pi.SetValue(obj, Convert.ChangeType(sr.ScalarValue, pi.PropertyType));
                }
            }
            return sr.EffectedLineCount;
        }
        
        public static int Update<T>(T obj) where T : class, new()
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
            return SqlHelper.Set(sql_str).EffectedLineCount;
        }
        
        public static int Delete<T>(int id)
        {
            return Delete<T>(id.ToString());
        }
        public static int Delete<T>(string id)
        {
            ModelAttribute ma = GetModelAttribute<T>();
            string sql_str = string.Format("delete from [{0}] where [{1}]='{2}'", ma.TableName, ma.PrimaryKey, id);
            return SqlHelper.Set(sql_str).EffectedLineCount;
        }
        #endregion

        #region Attribute相关函数
        public static string GetTableName<T>()
        {
            return GetModelAttribute<T>().TableName;
        }
        public static string GetPrimaryKey<T>()
        {
            return GetModelAttribute<T>().PrimaryKey;
        }
        public static bool GetIdentityInsert<T>()
        {
            return GetModelAttribute<T>().IdentityInsert;
        }
        public static ModelAttribute GetModelAttribute<T>()
        {
            ModelAttribute m = typeof(T).GetCustomAttribute<ModelAttribute>();
            if(m == null)
            {
                throw new Exception("模型类" + typeof(T).Name + "未使用ModelAttribute，[Model(TableName = \"t_article\", PrimaryKey = \"id\", IdentityInsert = true)]");
            }else
            {
                return m;
            }
        }
        public static PropertyInfo[] GetStorageablePropertys<T>()
        {
            PropertyInfo[] properties = typeof(T).GetProperties();// 获得此模型的公共属性
            List<PropertyInfo> result = new List<PropertyInfo>();
            foreach(PropertyInfo pi in properties)
            {
                FieldAttribute fa = pi.GetCustomAttribute<FieldAttribute>();
                if (fa == null || fa.Storageable) result.Add(pi);
            }
            return result.ToArray();
        }
        public static PropertyInfo[] GetWebablePropertys<T>(int group_code = 0)
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
        public static string ToJson(object obj, int group_code = 0)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings();
            setting.Converters.Add(new ModelConvert(group_code));
            return JsonConvert.SerializeObject(obj, Formatting.Indented, setting);
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

    public class ModelConvert : JsonConverter
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
            PropertyInfo[] pis = SqlHelperEx.GetWebablePropertys(current_type, group_code);

            writer.WriteStartObject();
            foreach(PropertyInfo pi in pis)
            {
                writer.WritePropertyName(pi.Name);
                writer.WriteRawValue(SqlHelperEx.ToJson(pi.GetValue(value), group_code));
            }
            writer.WriteEndObject();
        }
    }
    #endregion
}