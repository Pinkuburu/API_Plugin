using System;
using System.Collections;
using System.Reflection;
using System.Web;
using System.Text;

namespace ApiPlugin
{
    /// <summary>
    /// 机器人时间处理
    /// </summary>
    public class BotEventHandle : ApiPluginClass
    {
        // 调用标志位
        private static BindingFlags bindingFlag = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;

        // 调用内部方法
        public string CallAction(string action, Hashtable Data, string Bianma)
        {
            // 生成调用方法名
            string actionName = "Action_" + action;

            //获取方法的信息
            MethodInfo method = this.GetType().GetMethod(actionName, bindingFlag);

            // 方法是否存在
            if (method == null)
            {
                return "Not Action:" + action;
            }

            // 如果是私有方法判断是否登录
            if (method.IsPrivate && !this.IsLogin())
            {
                ShowMessage("Receive An Api : " + action + " ...Error");
                return "尚未登录成功，当前状态为：" + GetLoginStatus();
            }

            // 生成请求时间
            int time = 200;
            if (Data.Contains("time"))
            {
                try
                {
                    time = int.Parse(Data["time"].ToString());
                }
                catch
                {}
            }

            // 提交到方法的参数
            object[] parameters = new object[] { Data, time, Bianma };

            // 调用方法，用一个object接收返回值
            object returnValue = method.Invoke(this, bindingFlag, Type.DefaultBinder, parameters, null);

            // 返回返回的内容文本
            return returnValue.ToString();
        }

        // 判断是否登录
        public bool IsLogin()
        {
            return GetLoginStatus().Contains("登录成功");
        }

        /// <summary>
        /// 检查成员是否存在
        /// </summary>
        /// <param name="Data"></param>
        /// <param name="Member"></param>
        /// <returns></returns>
        private string CheckParameter(Hashtable Data, Array Member)
        {
            string[] retStr = { };

            foreach(string item in Member)
            {
                if(!Data.ContainsKey(item))
                {
                    retStr[1] = item;
                }
            }
            return "";
        
        }


        // 事件 发送消息
        private string Action_sendmessage(Hashtable Data, int time, string Bianma)
        {
            string Ret = string.Empty;
            // 判断参数是否存在



            ShowMessage("Receive An Api : SendMessage ...OK: " );
            string message = HttpUtility.UrlDecode(Data["message"].ToString(), Encoding.GetEncoding(Bianma));

            SendFriendMessage(Data["id"].ToString(), message, time);
            ShowMessage("Receive An Api : SendMessage ...OK");
            Ret = "参数提交成功!";

            return Ret;
        }
    }
}