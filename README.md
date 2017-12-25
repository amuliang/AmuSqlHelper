# AmuSqlHelper

## 介绍
AmuSqlHelper是一个C#环境下的数据库操作辅助工具，采用泛型思想，减少开发过程中的代码量。在大多数框架中，对model（数据模型）利用不够充分，而model上可大做文章。AmuSqlHelper的使用需要配合自定义的model，并且可以根据model创建数据库。

## 使用方法
AmuSqlHelper命名空间为AmuTools，助手类为SqlHelper。
``` CSharp
// 创建SqlHelper实体类，并提供连接数据库参数
SqlHelper db = new SqlHelper("服务器名", "数据库名", "用户名", "密码");
// 连接数据库参数可以之后指定
db.ServerName = "服务器名";
db.DatabaseName = "数据库名";
db.UserName = "用户名";
db.Password = "密码";
string connection_string = db.ConnectionString; // 数据库参数拼接结果
```
SqlHelper有两个重要的函数Get和Set，它们是进行数据库操作的核心函数，都返回SqlResult类型的值。Get函数用于查询操作，Set函数用于非查询操作。SqlResult有如下属性：

**Get方法返回后可使用的属性和泛型方法：**

    DataSet 数据表集合
    FirstTable 第一张数据表
    ScalarValue 标量值，即第一行第一列的值
    List<T> GetFirstTableList<T>() 实体类列表
    T GetFirstEntity<T>() 第一个实体类

**Set方法返回后可使用的属性：**

    ReturnValue 存储过程返回值
    OutputValue 存储过程输出值
    EffectedLineCount 受影响行数

``` CSharp
// 简单的Get查询操作
SqlResult result = db.Get("select * from user");
DataTable db = result.FirstTable;
int id = (int)result.ScalarValue;
// Get同样可以用于进行数据查询的存储过程
SqlParameter[] param = new SqlParameter[]
{
    db.CreateSqlParameter("@param1", SqlDbType.NVarChar, "apple"),
    db.CreateSqlParameter("@param2", SqlDbType.Int, 1),
};
result = db.Get("存储过程名", CommandType.StoredProcedure, param);
```

### 泛型
如果为每一个数据库操作都写sql语句显然是不现实的，因此需要做一定的封装。SqlHelper包含了一些基本的泛型函数
``` CSharp
T GetById<T>(string id)
T GetById<T>(int id)
int Insert<T>(T obj)
int Update<T>(T obj)
int Delete<T>(string id)
int Delete<T>(int id)
SqlResult GetPage<T>(string condition, string order_by, int skip, int take) // 注意，需要一个自带的存储过程sp_amu_getPageData
SqlResult Get<T>(string condition = "", string order_by = "", int size = 1000)
int GetCount<T>(string condition)
object GetMax<T>(string prop_name)
bool IsOne<T>(string condition)
```
要使用这些泛型函数，需要配合自定义的model才可以。model需要满足ModelAttribute描述，同时可以辅助FieldAttribute描述字段。下面给出一个较为完整的model示例

**ModelAttribute**

    TableName 表名
    PrimaryKey 主键名
    IdentityInsert 识别插入/主键自增

**FieldAttribute**

    Webable Web可见
    Strorageable 可数据库存储
    Nullable 可为空
    DataType 数据类型

``` CSharp
[Model(TableName = "t_admin", PrimaryKey = "id", IdentityInsert = true)]
public class Admin
{
    public int id { get; set; }

    public string username { get; set; }

    [Field(Webable = false, DataType = "varchar(50)")]
    public string password { get; set; }

    [Field(Webable = false)]
    public int role_id { get; set; }

    private AdminRole _role = null;
    [Field(Storageable = false)]
    public AdminRole role
    {
        get
        {
            if (_role == null)
            {
                _role = db.GetById<AdminRole>(this.role_id);
            }
            return _role;
        }
        set
        {
            _role = value;
            role_id = _role.id;
        }
    }

    public int status { get; set; }

    [Field(Webable = false)]
    public int is_delete { get; set; }

    [Field(Webable = false)]
    public int is_must { get; set; }

    public static Admin[] GetInitData()
    {
        return new Admin[]{
            new Admin { username = "admin", password = "123456", special_permission = "+100010", is_must = 1 }
        };
    }
}
```
从model的定义中可以看出，在SqlHelper的使用中，model至关重要。如果需要将model对象转为JSON字符串形式，使用ToJson函数，将自动屏蔽掉Webable为false的属性
``` CSharp
Admin admin = db.GetById<Admin>(1);
string json = db.ToJson(admin);
```

### 创建数据库
SqlHelper提供了数据库创建的方法。创建数据库，依赖模型创建表，依赖包含存储过程创建语句的文件创建存储过程。如果数据表已经存在，为了防止数据丢失，不会对字段进行修改和删除，但是会增加新的字段。如果数据表不存在，在创建表之后，还会将GetInitData函数的返回值插入到表中。
``` CSharp
// 需要指定根据哪些model类创建表
List<Type> types = new List<Type> { typeof(AdminRole), typeof(Admin) };
// 读取存放存储过程文件的文件夹，默认为*.text和*.sql
Dictionary<string, string> sps = db.GetSqlFiles(ctx.Server.MapPath("~/") + "Models/StoredProcedures/"));
db.CreateDataTable(types, sps);
```
在为服务器安装系统时，使用SqlHelper创建数据库将会非常方便。