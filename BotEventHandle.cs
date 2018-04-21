using System;
using System.Collections;
using System.Reflection;
using System.Web;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace ApiPlugin
{
    /// <summary>
    /// 机器人事件处理
    /// </summary>
    public class BotEventHandle : ApiPluginClass
    {
        /// <summary>
        /// 调用标志位
        /// </summary>
        private static BindingFlags bindingFlag = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>
        /// 动态调用事件方法
        /// </summary>
        /// <param name="action"></param>
        /// <param name="Data"></param>
        /// <returns></returns>
        public string CallAction(string action, Hashtable Data)
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

            // 获取方法的参数列表
            ParameterInfo[] parametersInfo = method.GetParameters();

            // 创建参数对象
            object[] parameterList = new object[parametersInfo.Length];

            // 不存在的参数列表
            List<string> notSetList = new List<string>();

            // 遍历参数
            string parametersName;
            for (int i = 0; i < parametersInfo.Length; i++)
            {
                // 获取参数名 小写
                parametersName = parametersInfo[i].Name.ToString().ToLower();

                // 判断参数是否提交
                if (!Data.ContainsKey(parametersName))
                {
                    // 判断是否必须参数
                    if (!parametersInfo[i].IsOptional)
                    {
                        notSetList.Add("(" + parametersInfo[i].ParameterType.Name + ")" + parametersName);
                        continue;
                    }

                    // 非必填就置空
                    Data.Add(parametersName, null);
                }

                // 转换到需要的类型
                if (Data[parametersName] != null)
                {
                    try
                    {
                        parameterList[i] = Convert.ChangeType(Data[parametersName], parametersInfo[i].ParameterType);
                    }
                    catch
                    {
                        notSetList.Add("(" + parametersInfo[i].ParameterType.Name + ")" + parametersName);
                    }
                }
                else
                {
                    parameterList[i] = parametersInfo[i].DefaultValue;
                }
            }

            // 如果有参数不存在就返回
            if (notSetList.Count() > 0)
            {
                return "缺少参数或参数不合法: " + String.Join(", ", notSetList.ToArray());
            }

            // 调用方法，用一个object接收返回值
            object returnValue = method.Invoke(this, bindingFlag, Type.DefaultBinder, parameterList, null);

            // 返回返回的内容文本
            return returnValue.ToString();
        }

        /// <summary>
        /// 判断是否登录
        /// </summary>
        /// <returns></returns>
        public bool IsLogin()
        {
            return GetLoginStatus().Contains("登录成功");
        }

        /// <summary>
        /// 事件 发送消息
        /// </summary>
        /// <param name="Data"></param>
        /// <param name="time"></param>
        /// <param name="Bianma"></param>
        /// <returns></returns>
        private string Action_sendmessage(uint Receiver, string Message, string Bianma)
        {
            Message = HttpUtility.UrlDecode(Message, Encoding.GetEncoding(Bianma));
            SendFriendMessage(Receiver, Message);

            ShowMessage("Receive An Api : SendMessage ...OK");
            return "参数提交成功!";
        }
    }
}