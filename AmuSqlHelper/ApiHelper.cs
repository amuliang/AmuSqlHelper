using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace AmuTools
{
    public class ApiHelper
    {
        private IApiUnit root { get; set; }
        private Action<ApiContext> _before = ctx => { };
        private Func<ApiContext, object, object> _after = (ctx, result) => { return result; };

        public void Register(IApiUnit i_api_unit)
        {
            this.root = i_api_unit;
        }

        public object Request(ApiContext ctx)
        {
            string[] actions = ctx.Url.Split('/');
            IApiUnit iau = this.root;

            if (actions.Length == 0) return null;
            if (actions[0] != this.root.GetName()) return null;
            for(int i = 1; i < actions.Length; i++)
            {
                string action = actions[i];
                if(iau.GetChildren().ContainsKey(action))
                {
                    iau = iau.GetChildren()[action];
                }
                else
                {
                    return null;
                }
            }
            this._before(ctx);
            iau.RunBefore(ctx);
            object result = iau.Run(ctx);
            result = iau.RunAfter(ctx, result);
            result = this._after(ctx, result);
            return result;
        }

        public ApiHelper Before(Action<ApiContext> before_action)
        {
            this._before = before_action;
            return this;
        }

        public ApiHelper After(Func<ApiContext, object, object> after_func)
        {
            this._after = after_func;
            return this;
        }
    }

    #region Api单元
    public class ApiUnit : ApiUnitBase
    {
        #region 私有属性
        private Action<ApiContext> _body { get; set; }
        private Action<ApiContext> _after { get; set; }
        private object _return_example { get; set; }
        private List<object> _return_examples { get; set; }
        #endregion

        #region 方法
        public ApiUnit(string name, string descrpition = "")
        {
            this._name = name;
            this._description = descrpition;
            this._args = new Dictionary<string, IArg>();
            this._body = ctx => {  };
            this._after = (ctx) => {  };
            this._children = new Dictionary<string, IApiUnit>();
        }

        public ApiUnit AddArg(IArg arg)
        {
            this._args.Add(arg.GetName(), arg);
            return this;
        }

        public ApiUnit Before(Action<ApiContext> before)
        {
            this._before = before;
            return this;
        }

        public ApiUnit Body(Action<ApiContext> body)
        {
            this._body = body;
            return this;
        }

        public ApiUnit After(Action<ApiContext> after)
        {
            this._after = after;
            return this;
        }

        public ApiUnit Return(string description, object example = default(object))
        {
            this._return_description = description;
            if (example != null) this._return_example = example;
            return this;
        }
        #endregion

        #region 重写抽象成员
        public override object Run(ApiContext ctx)
        {
            ctx.Args = new Dictionary<string, object>();
            foreach (string key in this._args.Keys)
            {
                IArg i_arg = this._args[key];
                ctx.Args[key] = i_arg.Test(ctx.Request[key]);
            }
            _body(ctx);
            return null;
        }

        protected override object PRunAfter(ApiContext ctx, object result)
        {
            this._after(ctx);
            return null;
        }

        public override object GetReturnExample()
        {
            return this._return_example;
        }

        public override object GetReturnExamples()
        {
            return this._return_examples;
        }
        #endregion
    }

    public class ApiUnit<Treturn> : ApiUnitBase
    {
        #region 私有属性
        private Func<ApiContext, Treturn> _body { get; set; }
        private Func<ApiContext, Treturn, object> _after { get; set; }
        private Treturn _return_example { get; set; }
        private List<Treturn> _return_examples { get; set; }
        #endregion

        #region 方法
        public ApiUnit(string name, string descrpition = "")
        {
            this._name = name;
            this._description = descrpition;
            this._args = new Dictionary<string, IArg>();
            this._body = ctx => { return default(Treturn); };
            this._after = (ctx, result) => { return result; };
            this._children = new Dictionary<string, IApiUnit>();
            this._return_example = (Treturn)Activator.CreateInstance(typeof(Treturn));
        }

        public ApiUnit<Treturn> AddArg(IArg arg)
        {
            this._args.Add(arg.GetName(), arg);
            return this;
        }

        public ApiUnit<Treturn> Before(Action<ApiContext> before)
        {
            this._before = before;
            return this;
        }

        public ApiUnit<Treturn> Body(Func<ApiContext, Treturn> body)
        {
            this._body = body;
            return this;
        }

        public ApiUnit<Treturn> After(Func<ApiContext, Treturn, object> after)
        {
            this._after = after;
            return this;
        }

        public ApiUnit<Treturn> Return(string description, Treturn example = default(Treturn))
        {
            this._return_description = description;
            if (example != null) this._return_example = example;
            return this;
        }
        #endregion

        #region 重写抽象成员
        public override object Run(ApiContext ctx)
        {
            ctx.Args = new Dictionary<string, object>();
            foreach (string key in this._args.Keys)
            {
                IArg i_arg = this._args[key];
                ctx.Args[key] = i_arg.Test(ctx.Request[key]);
            }
            return _body(ctx);
        }

        protected override object PRunAfter(ApiContext ctx, object result)
        {
            return this._after(ctx, (Treturn)result);
        }

        public override object GetReturnExample()
        {
            return this._return_example;
        }

        public override object GetReturnExamples()
        {
            return this._return_examples;
        }
        #endregion
    }

    public abstract class ApiUnitBase: IApiUnit
    {
        #region 私有属性
        protected string _name { get; set; }
        protected string _description { get; set; }
        protected Dictionary<string, IArg> _args { get; set; }
        protected Dictionary<string, IApiUnit> _children { get; set; }
        protected IApiUnit _parent { get; set; }
        protected string _return_description { get; set; }
        protected Action<ApiContext> _before = ctx => { };
        #endregion

        #region 方法
        #endregion

        #region 继承接口
        public void RunBefore(ApiContext ctx)
        {
            this._before(ctx);
        }

        public abstract object Run(ApiContext ctx);

        protected abstract object PRunAfter(ApiContext ctx, object result);

        public object RunAfter(ApiContext ctx, object result)
        {
            return PRunAfter(ctx, result);
        }

        public ApiUnit Register(string name, string descrpition = "")
        {
            ApiUnit au = new ApiUnit(name, descrpition);
            au._parent = this;
            this._Register(name, au);
            return au;
        }

        public ApiUnit<T> Register<T>(string name, string descrpition = "")
        {
            ApiUnit<T> au = new ApiUnit<T>(name, descrpition);
            au._parent = this;
            this._Register(name, au);
            return au;
        }

        private void _Register(string name, IApiUnit au)
        {
            if (this._children.ContainsKey(name))
            {
                throw new ApiException((int)APISTATUS.ERROR, string.Format("[{0}]分支下已经注册了[{1}]接口。", this._name, name));
            }
            this._children.Add(name, au);
        }

        public IApiUnit GetParent()
        {
            return _parent;
        }

        public string GetName()
        {
            return _name;
        }

        public string GetDescrpition()
        {
            return _description;
        }

        public Dictionary<string, IApiUnit> GetChildren()
        {
            return _children;
        }

        public Dictionary<string, IArg> GetArgs()
        {
            return this._args;
        }

        public Dictionary<string, object> GetApiJson()
        {
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["name"] = this.GetName();
            root["description"] = this.GetDescrpition();
            Dictionary < string, object> args = new Dictionary<string, object>();
            foreach(string key in this._args.Keys)
            {
                IArg i_arg = this._args[key];
                Dictionary<string, object> arg = new Dictionary<string, object>();
                arg["name"] = key;
                arg["type"] = i_arg.GetDataType();
                arg["description"] = i_arg.GetDescription();
                arg["default"] = i_arg.GetDefault();
                arg["level"] = i_arg.GetLevel().ToString();
                arg["example"] = i_arg.GetExample();
                arg["examples"] = i_arg.GetExamples();
                args[key] = arg;
            }
            root["args"] = args;
            Dictionary<string, object> children = new Dictionary<string, object>();
            foreach(string key in this._children.Keys)
            {
                IApiUnit iau = this._children[key];
                children[key] = iau.GetApiJson();
            }
            root["return_description"] = this.GetReturnDescription();
            root["return_example"] = this.GetReturnExample();
            root["return_examples"] = this.GetReturnExamples();
            root["children"] = children;
            return root;
        }

        public string GetReturnDescription()
        {
            return this._return_description;
        }

        public abstract object GetReturnExample();

        public abstract object GetReturnExamples();
        #endregion
    }

    public interface IApiUnit
    {
        IApiUnit GetParent();
        string GetName();
        string GetDescrpition();
        Dictionary<string, IArg> GetArgs();
        void RunBefore(ApiContext ctx);
        object Run(ApiContext ctx);
        object RunAfter(ApiContext ctx, object result);
        Dictionary<string, IApiUnit> GetChildren();
        ApiUnit Register(string name, string descrpition = "");
        ApiUnit<T> Register<T>(string name, string descrpition = "");
        Dictionary<string, object> GetApiJson();
        string GetReturnDescription();
        object GetReturnExample();
        object GetReturnExamples();
    }

    public class ApiContext
    {
        public string Url { get; set; }
        public Dictionary<string, object> Args { get; set; }
        public HttpSessionStateBase Session { get; set; }
        public HttpRequestBase Request { get; set; }
        public HttpResponseBase Response { get; set; }
    }
    #endregion

    #region 请求参数类
    public abstract class Arg<T>
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public ARGLEVEL Level { get; set; }
        public T Default { get; set; }
        public T Example { get; set; }
        public List<T> Examples { get; set; }
        public string Descrpition { get; set; }
        public int MaxLengh { get; set; }
        public APISTATUS LackStatus = APISTATUS.LACK_PARAM;
        public string LackMessage = "缺少参数";
        public APISTATUS TypeErrorStatus = APISTATUS.TYPEERROR_PARAM;
        public string TypeErrorMessage = "参数类型错误";
        public APISTATUS InvalidStatus = APISTATUS.INVALID_PARAM;
        public string InvalidMessage = "无效的参数";
        public Func<T, bool> InvalidTest = (value) => { return true; };

        public string GetName()
        {
            return this.Name;
        }

        public string GetDataType()
        {
            return this.DataType;
        }

        public string GetDescription()
        {
            return this.Descrpition;
        }

        public object GetDefault()
        {
            return this.Default;
        }

        public object GetExample()
        {
            return this.Example;
        }

        public object GetExamples()
        {
            return this.Examples;
        }

        public ARGLEVEL GetLevel()
        {
            return this.Level;
        }

        public object Test(object value)
        {
            string error_prefix = "错误参数" + this.Name + ":";
            // 首先检测是否为空值
            if (value == null || value.ToString() == "undefined")
            {
                // 为空
                if (this.Level == ARGLEVEL.MUST_VALID)
                {
                    throw new ApiException ((int)this.LackStatus, error_prefix + this.LackMessage);
                }
                else
                {
                    return this.Default;
                }
            }
            else
            {
                // 不为空
                // 检测是否为正确类型
                object temp;
                if (this.TypeTest(value.ToString(), out temp))
                {
                    // 类型正确
                    // 检测是否是合法有效的值
                    if (this.InvalidTest((T)temp))
                    {
                        // 有效
                        return temp;
                    }
                    else
                    {
                        // 无效
                        throw new ApiException((int)this.InvalidStatus, error_prefix + this.InvalidMessage);
                    }
                }
                else
                {
                    // 类型错误
                    if (this.Level == ARGLEVEL.FREE)
                    {
                        return this.Default;
                    }
                    else
                    {
                        throw new ApiException((int)this.TypeErrorStatus, error_prefix + this.TypeErrorMessage);
                    }
                }
            }
        }

        public abstract bool TypeTest(string value, out object result);
    }

    public class StrArg : Arg<string>, IArg
    {
        public StrArg()
        {
            this.DataType = "string 字符串";
            this.Default = "";
        }

        public override bool TypeTest(string value, out object result)
        {
            result = value;
            return true;
        }
    }

    public class ObjectArg<T> : Arg<T>, IArg
    {
        public ObjectArg()
        {
            this.DataType = typeof(T).ToString() + " 对象";
            this.Default = default(T);
        }

        public override bool TypeTest(string value, out object result)
        {
            try
            {
                result = JsonConvert.DeserializeObject<T>(value);
                return true;
            }
            catch(Exception e)
            {
                TypeErrorMessage = e.Message;
                result = this.Default;
                return false;
            }
        }
    }

    public class BoolArg : Arg<bool>, IArg
    {
        public BoolArg()
        {
            this.DataType = "boolean 布尔值";
            this.Default = false;
        }

        public override bool TypeTest(string value, out object result)
        {
            if (value == "1" || value == "true")
            {
                result = true;
                return true;
            }
            else if (value == "0" || value == "false")
            {
                result = false;
                return true;
            }
            else
            {
                result = this.Default;
                return false;
            }
        }
    }

    public class IntArg : Arg<int>, IArg
    {
        public IntArg()
        {
            this.DataType = "int 整型";
            this.Default = 0;
            TypeErrorStatus = APISTATUS.TYPEERROR_INT;
            TypeErrorMessage = "错误的INT类型";
        }

        public override bool TypeTest(string value, out object result)
        {
            int temp;
            if (value == "True" || value == "true")
            {
                result = 1;
                return true;
            }
            if (value == "False" || value == "false")
            {
                result = 0;
                return true;
            }
            if (int.TryParse(value, out temp))
            {
                result = temp;
                return true;
            }
            else
            {
                result = this.Default;
                return false;
            }
        }
    }

    public class DoubleArg : Arg<double>, IArg
    {
        public DoubleArg()
        {
            this.DataType = "double 浮点型";
            this.Default = 0;
            TypeErrorStatus = APISTATUS.TYPEERROR_INT;
            TypeErrorMessage = "错误的double类型";
        }

        public override bool TypeTest(string value, out object result)
        {
            double temp;
            if (double.TryParse(value, out temp))
            {
                result = temp;
                return true;
            }
            else
            {
                result = this.Default;
                return false;
            }
        }
    }

    public class TimeStampArg : Arg<long>, IArg
    {
        public TimeStampArg()
        {
            this.DataType = "timestamp 为长度不大于13的长整型（long）";
            this.Default = 0;
            TypeErrorStatus = APISTATUS.TYPEERROR_TIMESTAMP;
            TypeErrorMessage = "错误的时间戳格式";
        }

        public override bool TypeTest(string value, out object result)
        {
            // 还需要检测13个字符是否都为数字等，暂时没有判断
            if (value.Length <= 13)
            {
                long temp = 0;
                if (long.TryParse(value, out temp))
                {
                    result = temp;
                    return true;
                }
                else
                {
                    result = this.Default;
                    return false;
                }
            }
            else
            {
                result = this.Default;
                return false;
            }
        }
    }

    public interface IArg
    {
        string GetName();
        string GetDataType();
        string GetDescription();
        object GetDefault();
        object GetExample();
        object GetExamples();
        ARGLEVEL GetLevel();
        object Test(object value);
    }

    public enum ARGLEVEL
    {
        MUST_VALID, // 必须且有效
        VALID, // 不必须，但是必须有效
        FREE // 不必须
    }
    #endregion

    #region 返回结果类
    public class Result
    {
        public int status = (int)APISTATUS.OK;
    }
    public class IDResult : Result
    {
        public string id { get; set; }
    }
    public class ObjectResult : Result
    {
        public object data { get; set; }
    }
    public class MessageResult : Result
    {
        public object message { get; set; }
    }
    #endregion

    #region 异常类
    public class ApiException : ApplicationException
    {
        public int Status { get; set; }

        public ApiException() { }
        public ApiException(string message) : base(message) { }
        public ApiException(int status, string message) : base(message) { this.Status = status; }
        public override string Message
        {
            get
            {
                return base.Message;
            }
        }
    }
    #endregion

    #region 状态码
    public enum APISTATUS
    {
        OK = 10000,
        ERROR = 30000, // 错误

        // 缺少参数
        LACK_PARAM = 10100, // 缺少参数

        // 无效的参数
        INVALID_PARAM = 10201, //　无效的参数

        // 参数类型错误
        TYPEERROR_PARAM = 10501, // 参数类型错误
        TYPEERROR_INT = 10502, // 无效的int值
        TYPEERROR_TIMESTAMP = 10503, // 无效的时间戳值
                                     // 数据库
                                     // 搜索
                                     // 失败操作
        FAILED_INSERT = 10311, // 插入失败
        FAILED_UPDATE = 10312, // 更新失败
        FAILED_DELETE = 10313, // 删除失败

        // 没有
        NO_RESOURCES = 10301, // 找不到资源
        NO_PERMISSION = 10401, // 没有权限

        // 已经有
        //HAS_ACTION = 10602,  // 已经注册过的动作
        // 还没有
        //HASNOT_LOGIN = 10611, // 还没有登录

        // 迫使
        //FORCED_OFFLINE = 10701, // 被挤下线

        // 过期
        //OVERDUE_DATA = 10801,   // 过期的数据
                                // 还可以有过期的验证码
    }
    #endregion
}
