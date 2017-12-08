using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmuTools
{
    public class PostPage<T>
    {
        public int page { get; set; }
        public int count { get; set; }
        public int total { get; set; }
        public string condition { get; set; }
        public string orderby { get; set; }
        public List<T> list { get; set; }
    }
}
