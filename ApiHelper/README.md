# ApiHelper
ApiHelper设计的初衷是为了研究如何自动形成数据接口的帮助文档。对于自动形成API文档，通常的做法是对接口函数进行注释或者Attribute注解。本人受Javascript自由的编程思想所影响，希望能够用可执行代码实现，并较传统框架有更高的自由度。

## ApiHelper
ApiHelper类有如下方法
```CSharp
void Register(IApiUnit<TContext> i_api_unit) // 将ApiUnit单元作为根节点注册到ApiHelper
object Request(TContext ctx); // 发起请求，返回请求结果
ApiHelper<TContext> Before(Action<TContext> before_action); // 设置请求之前执行的代码
ApiHelper<TContext> After(Func<TContext, object, object> after_func); // 设置请求之后执行的代码
```
## ApiUnit
ApiUnit包含如下方法
```CSharp
ApiUnit<TContext> AddArg(IArg arg); // 添加参数描述
ApiUnit<TContext> AddArgs(List<IArg> args); // 添加多个参数描述
ApiUnit<TContext> Return(string description, object example = default(object)); // 设置返回值描述
ApiUnit<TContext> Before(Action<TContext> before); // 设置请求之前执行的代码
ApiUnit<TContext> Body(Action<TContext> body); // 设置请求主体代码
ApiUnit<TContext> After(Action<TContext> after); // 设置请求之后执行的代码
Dictionary<string, object> GetApiJson(); // 获取接口信息
```
ApiUnit是一个单元，各单元之间可以相连接，形成树形结构。例如现有根单元A，名称为“test_a”，在A上注册了一个单元B，名称为“test_b”，那么B为A的子树，访问B的地址为“test_a/test_b”。
``` CSharp
ApiHelper<MyApiContext> ah = new ApiHelper<MyApiContext>();

ApiUnit<MyApiContext> A = new ApiUnit<MyApiContext>("test_a");
A.Register<ObjectResult>("test_b")
 .Body( ctx => {
     return new ObjectResult { data = "data of test_b" };
 });

ah.Register(A);
object result = ah.Request(new MyApiContext {
    Url = "test_a/test_b"
});
```
## Arg
接口需要接收参数，则需要添加参数描述。
``` CSharp
// 为接口B添加name和age参数
A.Register<ObjectResult>("test_b", "测试接口B")
    .AddArg(new StrArg { Name = "name", Level = ARGLEVEL.FREE, Default = "李四", Descrpition = "名字" })
    .AddArg(new IntArg { Name = "age", Level = ARGLEVEL.FREE, Default = 35, Descrpition = "年龄" })
    .Body(ctx => {
        string name = (string)ctx.Args["name"];
        int age = (int)ctx.Args["age"];
        return new ObjectResult { data = name + age.ToString() };
    });
```
参数描述类型大概有如下几种

    StrArg
    IntArg
    DoubleArg
    BoolArg
    TimeStampArg
    ObjectArg<T>

参数等级ARGLEVEL有如下三种情况

    MUST_VALID: 必须提供该参数且参数值必须有效
    MUST: 必须提供该参数，如果参数值无效将使用默认值
    FREE: 可选参数，参数值无效将使用默认值

## 自动形成API文档
你应该已经注意到了，每一个接口都附带了大量的描述信息，这些信息足够形成详细的API文档
``` CSharp
Dictionary<string, object> api_json = A.GetApiJson();
```

## ApiContextBase
一般来说，Context应该包含Request、Session、Response等属性方法，而ApiContextBase仅提供了Url和Args两个属性，在具体应用中，使用者应该使用自己书写的ApiContextBase继承类。
``` CSharp
class MyApiContext : ApiContextBase
{
    public Dictionary<string, string> Request { get; set; }

    protected override object GetArg(string arg_name)
    {
        if (Request.ContainsKey(arg_name))
        {
            return Request[arg_name];
        }
        else
        {
            return null;
        }
    }
}
```

## ApiException
捕获接口自己抛出的异常，示例代码如下
``` CSharp
object result;
try
{
    Dictionary<string, string> request = new Dictionary<string, string>();
    request.Add("password", password);
    result = ah.Request(new MyApiContext { Url = "api/test", Request = request });
}
catch (ApiException e)
{
    result = new MessageResult { status = e.Status, message = e.Message };
}
```