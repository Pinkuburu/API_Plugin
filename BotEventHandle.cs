using QQRobot.SDK;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;

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
        private static BindingFlags bindingFlag = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>
        /// API插件对象
        /// </summary>
        private ApiPluginClass ApiPlugin = new ApiPluginClass();

        /// <summary>
        /// 对象初始化构造函数
        /// </summary>
        /// <param name="api"></param>
        public BotEventHandle(ApiPluginClass api)
        {
            ApiPlugin = api;
        }

        /// <summary>
        /// 动态调用事件方法
        /// </summary>
        /// <param name="action">被调用方法</param>
        /// <param name="Data">提交的数据</param>
        /// <param name="Bianma">数据编码</param>
        /// <returns></returns>
        public string CallAction(string action, Hashtable Data, string Bianma)
        {
            // 生成调用方法名
            string actionName = "Action_" + action.ToLower();

            //获取方法的信息
            MethodInfo method = this.GetType().GetMethod(actionName, bindingFlag);

            // 方法是否存在
            if (method == null)
            {
                // 尝试读取 重定向属性
                PropertyInfo property = this.GetType().GetProperty(actionName, bindingFlag);

                // 重定向存在就走重定向
                if (property != null)
                {
                    string actionNew = property.GetValue(this, null).ToString();
                    return CallAction(actionNew, Data, Bianma);
                }

                // 真不存在
                return "不存在的方法: " + action;

                /* 尝试调用SDK方法
                method = Main.GetType().GetMethod(action);
                Console.WriteLine("Main method : " + method);

                if (method == null)
                {
                    return "不存在的方法: " + action;
                }

                // 设置为重主类调用
                callObj = Main;
                Console.WriteLine("Main: " + method);
                */
            }

            // 如果是私有方法判断是否登录
            if (method.IsPrivate && !IsLogin())
            {
                ApiPlugin.ShowMessage("Receive An Api : " + action + " ...Failure");
                return "尚未登陆成功，当前状态为：" + ApiPlugin.GetLoginStatus();
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
                string parameterValue = string.Empty;
                if (Data[parametersName] != null)
                {
                    try
                    {
                        // 对取到的数据进行URL解码
                        parameterValue = HttpUtility.UrlDecode(Data[parametersName].ToString(), Encoding.GetEncoding(Bianma));
                        parameterList[i] = Convert.ChangeType(parameterValue, parametersInfo[i].ParameterType);
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
            object returnValue = string.Empty;
            try
            {
                returnValue = method.Invoke(this, bindingFlag, Type.DefaultBinder, parameterList, null);
                ApiPlugin.ShowMessage("Receive An Api : "+ action + " ...Success");
            }
            catch
            {
                returnValue = "Receive An Api : " + action + " ...Error";
                ApiPlugin.ShowMessage(returnValue.ToString());
            }

            // 返回返回的内容文本
            if(method.ReturnType.Name == "void")
            {
                return "参数提交成功";
            }
            return returnValue.ToString();
        }

        /// <summary>
        /// 判断是否登录
        /// </summary>
        /// <returns></returns>
        public bool IsLogin()
        {
            return ApiPlugin.GetLoginStatus().Contains("登陆成功");
        }

        #region 好友相关事件处理

        #endregion
        /// <summary>
        /// 添加好友
        /// </summary>
        /// <param name="QQ">QQ号码</param>
        /// <param name="Message">附言</param>
        /// <param name="OutSender">跟踪QQ</param>
        /// <param name="Bianma"></param>
        /// <returns></returns>
        private string Action_addfriend(UInt32 QQ, string Message, UInt32 OutSender = 0, string Bianma = null)
        {
            ApiPlugin.AddFriend(QQ, Message, OutSender);
            return "参数提交成功!";
        }

        /// <summary>
        /// 清空加好友列队
        /// </summary>
        /// <returns></returns>
        private string Action_cleanaddfriendqueue()
        {
            ApiPlugin.CleanAddFriendQueue();
            return "参数提交成功!";
        }
        
        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="Receiver">接收QQ号码</param>
        /// <param name="Message">发送的消息</param>
        /// <param name="Bianma">消息编码</param>
        /// <returns></returns>
        private string Action_sendfriendmessage(uint Receiver, string Message, string Bianma)
        {
            Message = HttpUtility.UrlDecode(Message, Encoding.GetEncoding(Bianma));
            ApiPlugin.SendFriendMessage(Receiver, Message);
            return "参数提交成功!";
        }

        /// <summary>
        /// 重写到 发送消息事件
        /// </summary>
        private string Action_sendmessage { get { return "sendfriendmessage"; } }

        /// <summary>
        /// 加群
        /// </summary>
        /// <param name="ClusterNumber">群号</param>
        /// <param name="Message">附言</param>
        /// <param name="Bianma"></param>
        /// <returns></returns>
        private string Action_addcluster(uint ClusterNumber, string Message, string Bianma)
        {
            
            ApiPlugin.AddCluster(ClusterNumber, Message);
            return "参数提交成功!";
        }
    }
}