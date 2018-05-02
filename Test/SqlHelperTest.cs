using System;
using AmuTools;
using System.Collections.Generic;
using Xunit;

namespace Test
{
    public class SqlHelperTest
    {
        [Fact(DisplayName = "获取属性的正确性")]
        public void SqlHelperTest1()
        {
            //
            SqlHelper sh = new SqlHelper();
            ParsedModel pm = SqlHelper.GetParsedModel(typeof(Person));

            // 测试
            Assert.True(pm.StorageableFields.ContainsKey("id"));
            Assert.True(pm.StorageableFields.ContainsKey("name"));
            Assert.False(pm.StorageableFields.ContainsKey("private_name"));
            Assert.False(pm.StorageableFields.ContainsKey("just_read_name"));
            Assert.False(pm.StorageableFields.ContainsKey("static_name"));
        }

    }



    [Model(TableName = "person", PrimaryKey = "id")]
    class Person
    {
        public string id { get; set; }
        public string name { get; set; }
        private string private_name { get; set; }
        public string just_read_name { get; }
        static public string static_name { get; set; }
        
    }
}
