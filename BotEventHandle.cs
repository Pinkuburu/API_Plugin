using QQRobot.SDK.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;
using System.Drawing;

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
        /// 方法列表
        /// </summary>
        private List<String> ActionList = new List<string>();

        /// <summary>
        /// 对象初始化构造函数
        /// </summary>
        /// <param name="api"></param>
        public BotEventHandle(ApiPluginClass api)
        {
            // 保存API对象
            ApiPlugin = api;

            // 获取自身方法列表
            MemberInfo[] methods = this.GetType().GetMethods(bindingFlag);

            // 整理符合规则的方法名
            foreach (MemberInfo MethodItem in methods)
            {
                // Action_ 开头的方法
                if (MethodItem.Name.Length > 8 && MethodItem.Name.Substring(0, 7) == "Action_")
                {
                    ActionList.Add(MethodItem.Name);
                }
                // 获取 重定向的方法
                else if (MethodItem.Name.Length > 12 && MethodItem.Name.Substring(0, 11) == "get_Action_")
                {
                    ActionList.Add(MethodItem.Name.Substring(4));
                }
            }


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

            // 判断方法是否在数组中(不区分大小写)
            if (!ActionList.ToArray().Contains(actionName, StringComparer.OrdinalIgnoreCase))
            {
                return "不存在的方法: " + action;
            }

            // 获取方法名(真实的)
            actionName = ActionList.Find(delegate (string name)
            {
                return name.ToLower() == actionName.ToLower();
            });

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
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                returnValue = "Receive An Api : " + action + " ...Error";
                ApiPlugin.ShowMessage(returnValue.ToString());
            }

            // 返回的内容文本
            string retString = string.Empty;
            switch (method.ReturnType.Name)
            {
                // 正常应该返回此数据类型
                case "RetJsonData":
                    retString = JSON.Stringify(returnValue);
                    break;

                // 空类型
                case "Void":
                    retString = JSON.Stringify(new RetJsonData());
                    break;

                // 字符串类型
                case "String":
                    retString = JSON.Stringify(new RetJsonData() { Msg = returnValue.ToString() });
                    break;

                // 整数型
                case "Int32":
                    retString = JSON.Stringify(new RetJsonData() { Code = Convert.ToInt32(returnValue), Msg = "" });
                    break;

                // 其他未知
                default:
                    retString = JSON.Stringify(new RetJsonData() { Data = returnValue });
                    break;
            }

            return retString;
        }

        /// <summary>
        /// 判断是否登录
        /// </summary>
        /// <returns></returns>
        public bool IsLogin()
        {
            return ApiPlugin.GetLoginStatus().Contains("登陆成功");
        }

        #region 自身事件处理

        /// <summary>
        /// 更改昵称
        /// </summary>
        /// <param name="Nick">新昵称</param>
        private void Action_ChangeNick(String Nick)
        {
            ApiPlugin.ChangeNick(Nick);
        }

        /// <summary>
        /// 更改机器人状态
        /// </summary>
        /// <param name="Status">状态代码</param>
        /// <param name="StatusText">状态文本</param>
        private void Action_ChangeStatus(String Status, String StatusText)
        {
            ApiPlugin.ChangeStatus(Status, StatusText);
        }

        /// <summary>
        /// 获取登陆状态
        /// </summary>
        public string Action_GetLoginStatus()
        {
            return ApiPlugin.GetLoginStatus();
        }

        /// <summary>
        /// 获取QQ登陆口令
        /// </summary>
        /// <returns>口令</returns>
        private string Action_GetRobotClientKey()
        {
            return ApiPlugin.GetRobotClientKey();
        }

        /// <summary>
        /// 获取状态信息
        /// </summary>
        /// <returns></returns>
        private string Action_GetStatusText()
        {
            return ApiPlugin.GetStatusText();
        }

        /// <summary>
        /// 修改签名
        /// </summary>
        /// <param name="Signature">新签名</param>
        private void Action_ModifySignature(String Signature)
        {
            ApiPlugin.ModifySignature(Signature);
        }

        #endregion 自身事件结束

        #region 好友事件处理

        /// <summary>
        /// 开启/关闭好友回复
        /// </summary>
        /// <param name="Open">开启/关闭</param>
        private void Action_Friend(Boolean Open)
        {
            ApiPlugin.Friend(Open);
        }

        /// <summary>
        /// 添加好友
        /// </summary>
        /// <param name="QQ">QQ号码</param>
        /// <param name="Message">附言</param>
        /// <param name="OutSender">跟踪QQ</param>
        private void Action_AddFriend(UInt32 QQ, string Message, UInt32? OutSender = null)
        {
            if(OutSender == null)
            {
                ApiPlugin.AddFriend(QQ, Message);
            } else
            {
                ApiPlugin.AddFriend(QQ, Message, (UInt32)OutSender);
            }
        }

        /// <summary>
        /// 清空加好友队列
        /// </summary>
        /// <returns></returns>
        private void Action_CleanAddFriendQueue()
        {
            ApiPlugin.CleanAddFriendQueue();
        }

        /// <summary>
        /// 创建临时会话
        /// </summary>
        /// <param name="ClusterNum">群号码</param>
        /// <param name="QQNumber">QQ号码</param>
        /// <returns>状态</returns>
        private string Action_CreateSession(UInt32 ClusterNum, UInt32 QQNumber)
        {
            return ApiPlugin.CreateSession(ClusterNum, QQNumber);
        }

        /// <summary>
        /// 获取好友列表
        /// </summary>
        /// <returns>列表数据</returns>
        private RetJsonData Action_GetFriendList()
        {
            Sdk_ContactInfo friendList = ApiPlugin.GetFriendList();
            
            if(friendList == null)
            {
                return new RetJsonData() { Code = 404, Msg = "未获取到列表" };
            }

            // 遍历所有成员
            List<Hashtable> contactList = new List<Hashtable>();
            foreach (KeyValuePair<uint, Sdk_ContactInfo> item in friendList)
            {
                var _item = item.Value;
                Hashtable memberInfo = new Hashtable() {
                    { "QQNumber", _item.QQNumber },
                    { "Nick", _item.Nick },
                    { "Gender", _item.Gender },
                    { "Age", _item.Age }
                };
                contactList.Add(memberInfo);
            }

            return new RetJsonData() { Data = contactList };


        }

        /// <summary>
        /// 获取用户的详细资料
        /// </summary>
        /// <param name="QQNumber">QQ号码</param>
        /// <returns></returns>
        private RetJsonData Action_GetUserInfo(UInt32 QQNumber)
        {
            Sdk_UserInfo_D userInfo = ApiPlugin.GetUserInfo(QQNumber);

            if(userInfo == null)
            {
                return new RetJsonData() { Code = 404, Msg = "未找到好友" };
            }

            Hashtable memberInfo = new Hashtable() {
                {"Zodiac", userInfo.Zodiac },
                {"Horoscope", userInfo.Horoscope },
                {"College", userInfo.College },
                {"LiveHome", userInfo.LiveHome },
                {"Description", userInfo.Description },
                {"Mobile", userInfo.Mobile },
                {"HomePage", userInfo.HomePage },
                {"Email", userInfo.Email },
                {"EnglistName", userInfo.EnglistName },
                {"Name", userInfo.Name },
                {"Gender", userInfo.Gender },
                {"Age", userInfo.Age },
                {"Telephone", userInfo.Telephone },
                {"Address", userInfo.Address },
                {"ZipCode", userInfo.ZipCode },
                {"OldHome", userInfo.OldHome },
                {"Nick", userInfo.Nick },
                {"QQ", userInfo.QQ },
                {"Blood", userInfo.Blood },
                {"QQAge", userInfo.QQAge }
            };
            memberInfo.Add("BirthDay", userInfo.BirthDay.ToString());

            return new RetJsonData() { Data = memberInfo };
        }

        /// <summary>
        /// 发送好友消息
        /// </summary>
        /// <param name="Receiver">接收者，支持“,”分割，支持Admin与All</param>
        /// <param name="Message">消息</param>
        /// <param name="Time">间隔时间，单位：毫秒</param>
        private void Action_SendFriendMessage(String Receiver, String Message, Int32? Time = null)
        {
            if(Time == null)
            {
                Time = 200;
            }
            ApiPlugin.SendFriendMessage(Receiver, Message, (int)Time);
        }

        /// <summary>
        /// 发送临时会话消息
        /// </summary>
        /// <param name="Receiver">临时会话接收者，支持“,”分割。</param>
        /// <param name="Message">消息</param>
        /// <param name="Time">间隔时间，单位：毫秒</param>
        private void Action_SendTempSession(String Receiver, String Message, Int32? Time = null)
        {
            if (Time == null)
            {
                Time = 200;
            }
            ApiPlugin.SendTempSession(Receiver, Message, (int)Time);
        }

        /// <summary>
        /// 发送弹窗
        /// </summary>
        /// <param name="Receiver">接收者，支持“,”分割，支持All</param>
        /// <param name="Time">间隔时间，单位：毫秒</param>
        private void Action_SendVibration(String Receiver, Int32? Time = null)
        {
            if (Time == null)
            {
                Time = 200;
            }
            ApiPlugin.SendVibration(Receiver, (int)Time);
        }

        /// <summary>
        /// 设置好友备注
        /// </summary>
        /// <param name="QQ">QQ号码</param>
        /// <param name="Remark">备注信息</param>
        private void Action_SetFriendRemark(UInt32 QQ, String Remark)
        {
            ApiPlugin.SetFriendRemark(QQ, Remark);
        }

        #endregion 好友事件结束

        #region 群事件处理

        /// <summary>
        /// 开启/关闭群回复
        /// </summary>
        /// <param name="Open">是否开启</param>
        private void Action_Cluster(Boolean Open)
        {
            ApiPlugin.Cluster(Open);
        }

        /// <summary>
        /// 加群
        /// </summary>
        /// <param name="ClusterNumber">群号</param>
        /// <param name="Message">附言</param>
        /// <param name="OutSender">跟踪QQ 可选</param>
        private void Action_AddCluster(UInt32 ClusterNumber, String Message, UInt32? OutSender = null)
        {
            if(OutSender == null)
            {
                ApiPlugin.AddCluster(ClusterNumber, Message);
            } else
            {
                ApiPlugin.AddCluster(ClusterNumber, Message, (UInt32)OutSender);
            }
        }

        /// <summary>
        /// 清空加群队列
        /// </summary>
        private void Action_CleanAddClusterQueue()
        {
            ApiPlugin.CleanAddClusterQueue();
        }

        /// <summary>
        /// 更改成员名片
        /// </summary>
        /// <param name="ClusterNum">群号</param>
        /// <param name="MemberQQ">成员QQ</param>
        /// <param name="CardName">新名片</param>
        private void Action_ChangeClusterMemberCard(UInt32 ClusterNum, UInt32 MemberQQ, String CardName)
        {
            ApiPlugin.ChangeClusterMemberCard(ClusterNum, MemberQQ, CardName);
        }

        /// <summary>
        /// 退出指定群
        /// </summary>
        /// <param name="ClusterNumber">群号</param>
        /// <returns></returns>
        private void Action_ExitCluster(UInt32 ClusterNumber)
        {
            ApiPlugin.ExitCluster(ClusterNumber);
        }

        /// <summary>
        /// 获取单个群信息
        /// </summary>
        /// <param name="ClusterNumber">群号码</param>
        /// <returns>群信息</returns>
        private RetJsonData Action_GetClusterInfo(UInt32 ClusterNumber)
        {
            // 获取群信息
            Sdk_ClusterInfo ClusterInfo = ApiPlugin.GetClusterInfo(ClusterNumber);

            // 查找失败
            if (ClusterInfo == null)
            {
                return new RetJsonData() { Code = 404, Msg = "获取失败" };
            }

            // 结构对象
            Hashtable retData = new Hashtable() {
                { "ClusterNum", ClusterInfo.ClusterNum },
                { "ClusterName", ClusterInfo.ClusterName },
                { "Description", ClusterInfo.Description },
                { "Notice", ClusterInfo.Notice },
                { "Creator", ClusterInfo.Creator },
                { "NowNum", ClusterInfo.NowNum }
            };

            return new RetJsonData() { Data = retData };
        }

        /// <summary>
        /// 获取单个群成员列表
        /// </summary>
        /// <param name="ClusterNumber">群号码</param>
        /// <returns></returns>
        private RetJsonData Action_GetClusterMembers(UInt32 ClusterNumber)
        {
            // 获取群信息
            Sdk_ClusterInfo ClusterInfo = ApiPlugin.GetClusterInfo(ClusterNumber);

            if(ClusterInfo == null)
            {
                return new RetJsonData() { Code = 404, Msg = "未找到群" };
            }

            // 遍历所有成员
            List<Hashtable> memberList = new List<Hashtable>();
            foreach (KeyValuePair<uint, Sdk_MemberInfo> item in ClusterInfo.MemberInfo)
            {
                //item.Key;
                var _item = item.Value;
                Hashtable memberInfo = new Hashtable() {
                    { "QQ", _item.QQ },
                    { "Nick", _item.Nick },
                    { "RemarksName", _item.RemarksName },
                    { "IsCreator", _item.IsCreator },
                    { "IsAdmin", _item.IsAdmin },
                    { "Gender", _item.Gender },
                    { "Age", _item.Age },
                    { "LastSpeak", _item.LastSpeak.ToString("yyyy-MM-dd")}
                };

                memberList.Add(memberInfo);
            }

            return new RetJsonData() { Data = memberList };
        }

        /// <summary>
        /// 获取群列表
        /// </summary>
        /// <returns></returns>
        private RetJsonData Action_GetClusterList()
        {
            // 获取群信息
            Sdk_ClusterInfo ClusterInfo = ApiPlugin.GetClusterList();

            if(ClusterInfo == null)
            {
                return new RetJsonData() { Code = 404, Msg = "未获取到群列表" };
            }

            // 遍历所有成员
            List<Hashtable> clusterList = new List<Hashtable>();
            foreach (KeyValuePair<uint, Sdk_ClusterInfo> item in ClusterInfo)
            {
                //item.Key;
                var _item = item.Value;
                Hashtable clusterInfo = new Hashtable() { };
                clusterInfo.Add("ClusterNum", _item.ClusterNum);
                clusterInfo.Add("ClusterName", _item.ClusterName);
                clusterInfo.Add("Description", _item.Description);
                clusterInfo.Add("Notice", _item.Notice);
                clusterInfo.Add("Creator", _item.Creator);
                clusterInfo.Add("NowNum", _item.NowNum);
                clusterList.Add(clusterInfo);
            }

            return new RetJsonData() { Data = clusterList };
        }

        /// <summary>
        /// 获取加群验证码
        /// </summary>
        /// <returns></returns>
        private RetJsonData Action_GetClusterVerify(UInt32 ClusterNumber)
        {
            // 获取验证码图片
            Image verifyImage = ApiPlugin.GetClusterVerify(ClusterNumber);

            return new RetJsonData() { Data = verifyImage };
        }

        /// <summary>
        /// 邀请好友进群（必须为群管理员，并且至少一方为好友）
        /// </summary>
        /// <param name="Cluster">群号【支持“,”分割，支持All】</param>
        /// <param name="QQ">好友号码【支持“,”分割】</param>
        private void Action_InviteMember(String Cluster, String QQ)
        {
             ApiPlugin.InviteMember(Cluster, QQ);
        }

        /// <summary>
        /// 判断是否为管理员
        /// </summary>
        /// <param name="ClusterNumber">群号码</param>
        /// <param name="MemberQQ">群成员号码</param>
        /// <returns>0表示普通用户，1表示管理员，2表示创始人，-1表示未找到该群号，-2表示未找到该成员，-9表示机器人没有注册该方法</returns>
        private Int32 Action_IsClusterAdmin(UInt32 ClusterNumber, UInt32 MemberQQ)
        {
            return ApiPlugin.IsClusterAdmin(ClusterNumber, MemberQQ);
        }

        /// <summary>
        /// T出群成员（必须为群管理员）
        /// </summary>
        /// <param name="Cluster">群号【支持“,”分割，支持All】</param>
        /// <param name="Member">成员号码【支持“,”分割】</param>
        private void Action_KickOutMember(String Cluster, String Member)
        {
            ApiPlugin.KickOutMember(Cluster, Member);
        }

        /// <summary>
        /// 发送群消息
        /// </summary>
        /// <param name="Receiver">群号</param>
        /// <param name="Message">消息</param>
        /// <param name="Time">间隔时间，单位：毫秒</param>
        private void Action_SendClusterMessage(String Receiver, String Message, UInt32? Time = null)
        {
            if (Time == null)
            {
                Time = 200;
            }

            ApiPlugin.SendClusterMessage(Receiver, Message, (int)Time);
        }

        /// <summary>
        /// 发送加群验证码
        /// </summary>
        /// <param name="Cluster">群号</param>
        /// <param name="VerifyCode">验证码</param>
        /// <returns>是否成功</returns>
        private Boolean Action_SendClusterVerify(UInt32 Cluster, String VerifyCode)
        {
            return ApiPlugin.SendClusterVerify(Cluster, VerifyCode);
        }

        /// <summary>
        /// 发送临时群消息
        /// </summary>
        /// <param name="ClusterNumber">临时群群号</param>
        /// <param name="Message">消息</param>
        private void Action_SendTempClusterMessage(UInt32 ClusterNumber, String Message)
        {
            ApiPlugin.SendTempClusterMessage(ClusterNumber, Message);
        }

        #endregion 群事件结束

        #region 机器人事件处理

        /// <summary>
        /// 清空统计信息
        /// </summary>
        public void Action_CleanCountInfo()
        {
            ApiPlugin.CleanCountInfo();
        }

        /// <summary>
        /// 清理内存
        /// </summary>
        public void Action_CleanMemory()
        {
            ApiPlugin.CleanMemory();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Action_Dispose()
        {
            ApiPlugin.Dispose();
        }

        /// <summary>
        /// 退出机器人
        /// </summary>
        public void Action_ExitRobot()
        {
            ApiPlugin.ExitRobot();
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string Action_GetCountText()
        {
            return ApiPlugin.GetCountText();
        }

        /// <summary>
        /// 获取版本信息
        /// </summary>
        /// <returns>版本文本</returns>
        public string Action_GetVersionText()
        {
            return ApiPlugin.GetVersionText();
        }

        /// <summary>
        /// 登陆机器人
        /// </summary>
        public void Action_LoginRobot()
        {
            ApiPlugin.LoginRobot();
        }

        /// <summary>
        /// 发送登陆验证码
        /// </summary>
        /// <param name="VerifyCode">验证码</param>
        /// <returns>是否成功</returns>
        public Boolean Action_SendVerify(String VerifyCode)
        {
            return ApiPlugin.SendVerify(VerifyCode);
        }

        /// <summary>
        /// 重启机器人
        /// </summary>
        public void Action_ResetRobot()
        {
            ApiPlugin.ResetRobot();
        }

        /// <summary>
        /// 重新开启机器人
        /// </summary>
        public void Action_ReStartRobot()
        {
            ApiPlugin.ReStartRobot();
        }

        /// <summary>
        /// 更新机器人信息
        /// </summary>
        public void Action_UpdateRobotInfo()
        {
            ApiPlugin.UpdateRobotInfo();
        }

        /// <summary>
        /// 更新配置文件
        /// </summary>
        public void Action_UpdateConfig()
        {
            ApiPlugin.UpdateConfig();
        }

        #endregion 机器人事件结束

        /// <summary>
        /// 重写到 发送消息事件
        /// </summary>
        private string Action_SendMessage { get { return "SendFriendMessage"; } }
    }
}