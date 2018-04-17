using System;
using AmuTools;
using System.Collections.Generic;
using Xunit;

namespace Test
{
    public class ApiUnitTest
    {
        [Fact(DisplayName = "参数默认值测试")]
        public void ApiUnitTest1()
        {
            // 注册接口
            ApiHelper<MyApiContext> ah = new ApiHelper<MyApiContext>();
            ApiUnit<MyApiContext> root = new ApiUnit<MyApiContext>("api");
            ah.Register(root);

            root.Register<ObjectResult>("test1", "测试1接口")
                .AddArg(new StrArg { Name = "name", Level = ARGLEVEL.FREE, Default = "李四", Descrpition = "名字" })
                .AddArg(new IntArg { Name = "age", Level = ARGLEVEL.FREE, Default = 35, Descrpition = "年龄" })
                .Body(ctx => {
                    string name = (string)ctx.Args["name"];
                    int age = (int)ctx.Args["age"];
                    return new ObjectResult { data = name + age.ToString() };
                });

            // 测试接口
            Dictionary<string, string> request = new Dictionary<string, string>();
            request.Add("name", "张三");
            object result = ah.Request(new MyApiContext { Url = "api/test1", Request = request });
            Assert.Equal("张三35", ((ObjectResult)result).data.ToString());
        }

        [Theory(DisplayName = "无效的参数")]
        [InlineData("2345d")]
        [InlineData("12345tdgbrgjhyt6utij67435646u")]
        public void ApiUnitTest2(string password)
        {
            // 注册接口
            ApiHelper<MyApiContext> ah = new ApiHelper<MyApiContext>();
            ApiUnit<MyApiContext> root = new ApiUnit<MyApiContext>("api");
            ah.Register(root);

            root.Register<ObjectResult>("test")
                .AddArg(new StrArg { Name = "password", Descrpition = "密码", InvalidTest = value => {
                    return value.Length > 5 && value.Length < 20;
                } })
                .Body(ctx => {
                    string name = (string)ctx.Args["name"];
                    int age = (int)ctx.Args["age"];
                    return new ObjectResult { data = name + age.ToString() };
                });

            // 测试接口
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
            Assert.Equal((int)APISTATUS.INVALID_PARAM, ((Result)result).status);
        }

    }




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
}
