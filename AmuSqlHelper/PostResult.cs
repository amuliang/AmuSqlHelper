using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmuTools
{
    public class PostResult
    {
        public int status { get; set; }
        public string message { get; set; }
        public object data { get; set; }

        public static PostResult OK(string message, object data)
        {
            return new PostResult()
            {
                status = 200,
                message = message,
                data = data
            };
        }

        public static PostResult OK(string message)
        {
            return PostResult.OK(message, null);
        }

        public static PostResult OK(object data)
        {
            return PostResult.OK("OK", data);
        }

        public static PostResult OK()
        {
            return PostResult.OK(null);
        }

    //    public JsonResult JsonResult()
    //    {
    //        JsonResult jr = new JsonResult();
    //        jr.Data = this;
    //        return jr;
    //    }
    }
}
