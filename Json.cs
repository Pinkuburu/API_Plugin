using System;
using System.Collections;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace ApiPlugin
{
    /// <summary>
    /// 解析JSON
    /// </summary>
    public static class JSON
    {
        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonString"></param>
        /// <returns></returns>
        public static T Parse<T>(string jsonString)
        {
            return new JavaScriptSerializer().Deserialize<T>(jsonString);
        }

        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="jsonObject"></param>
        /// <returns></returns>
        public static string Stringify(object jsonObject)
        {
            return new JavaScriptSerializer().Serialize(jsonObject);
        }
    }

    /// <summary>
    /// 返回数据格式
    /// </summary>
    public class RetJsonData
    {
        /// <summary>
        /// 状态码
        /// </summary>
        public int Code = 200;

        /// <summary>
        /// 消息文本
        /// </summary>
        public string Msg = "请求成功";

        /// <summary>
        /// 数据内容
        /// </summary>
        public Object Data;
    }
}
