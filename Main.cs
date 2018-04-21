using QQRobot.SDK;
using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;
using System.Windows.Forms;

namespace ApiPlugin
{
    /// <summary>
    /// API插件类库
    /// </summary>
    public class ApiPluginClass : ClientSdk
    {
        /// <summary>
        /// 服务对象
        /// </summary>
        private static Socket server;

        /// <summary>
        /// 客户端连接列表
        /// </summary>
        private static Hashtable clients = new Hashtable();

        /// <summary>
        /// GET轮询处理对象
        /// </summary>
        private GetCycle GetCycle;

        /// <summary>
        /// 机器人事件处理对象
        /// </summary>
        private static BotEventHandle eventObj = new BotEventHandle();

        /// <summary>
        /// 默认读取缓冲区长度
        /// </summary>
        byte[] recvBuf = new byte[64 * 1000];

        /// <summary>
        /// 客户端数量
        /// </summary>
        int clientNum;

        /// <summary>
        /// 初始化插件
        /// </summary>
        public override void Init()
        {
            // 创建GET轮询
            if (GetCycle == null)
            {
                GetCycle = new GetCycle(this);
            }

            // 创建服务对象
            if (server == null)
            {
                ListenThreadRun();
            }
        }

        /// <summary>
        /// 事件: 关闭插件
        /// </summary>
        public override void OnClosePlug()
        {
            if (server != null)
            {
                server.Close();
                server = null;
                ShowMessage("API Server Is Closed Succeed.");
            }
        }

        /// <summary>
        /// 监听...
        /// </summary>
        private void ListenThreadRun()
        {
            try
            {
                IPEndPoint localEP = new IPEndPoint(IPAddress.Any, RobotConfig.ApiPort);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                server.Bind(localEP);
                server.Listen(200);
                ShowMessage("API Server Is Runnning. ListenIng [Port:" + RobotConfig.ApiPort + "]");
                server.BeginAccept(new AsyncCallback(OnAccept), server);
            }
            catch
            {
                ShowMessage("Listen Port [" + RobotConfig.ApiPort + "] Fail.", ConsoleColor.Red);
                RobotConfig.ApiPort = new Random().Next(1000, 65525);
                ShowMessage("Change Port [" + RobotConfig.ApiPort + "] Succeed");
                ListenThreadRun();
            }
        }

        /// <summary>
        /// 套接字: 客户端连接
        /// </summary>
        /// <param name="asynResult"></param>
        private void OnAccept(IAsyncResult asynResult)
        {
            try
            {
                Socket socket = server.EndAccept(asynResult);
                clientNum++;
                string key = clientNum.ToString();
                clients.Add(key, socket);
                socket.BeginReceive(recvBuf, 0, 64 * 1000, SocketFlags.None, new AsyncCallback(ReceiveCallback), key);
            }
            catch
            {

            }
            finally
            {
                if (server!=null)
                {
                    server.BeginAccept(new AsyncCallback(OnAccept), server);
                }
            }
        }

        /// <summary>
        /// 套接字: 收到消息的回调
        /// </summary>
        /// <param name="asResult"></param>
        private void ReceiveCallback(IAsyncResult asResult)
        {
            string key = asResult.AsyncState.ToString();
            Socket client = (Socket)clients[key];
            try
            {
                int count = client.EndReceive(asResult);
                if (count > 0)
                {
                    string str = Encoding.UTF8.GetString(recvBuf, 0, count);

                    // 处理API消息
                    APISelect(client, str, key);
                }
                else
                {
                    clients.Remove(key);
                }
            }
            catch
            {
                clients.Remove(key);
            }
        }

        /// <summary>
        /// 处理API消息内容
        /// </summary>
        /// <param name="client"></param>
        /// <param name="str"></param>
        /// <param name="key"></param>
        private void APISelect(Socket client, string str, string key)
        {
            // 判断GET请求是否合法
            if (str.Length < 10)
            {
                ShowMessage("Receive An Error(1) Data...", ConsoleColor.Red);
                SocketSendMsg(client, "Not Data!", "Gb2312", key);
                return;
            }

            // 判断请求路径
            if (str.Substring(0, 9).ToLower() != "get /api?")
            {
                // 是否请求图标
                if (str.Substring(0, 16).ToLower() == "get /favicon.ico")
                {
                    SocketSendMsg(client, "Favicon.ico", "Gb2312", key, "SysMsg");
                    return;
                }

                // 其他请求则报错
                ShowMessage("Receive An Error(2) Data...", ConsoleColor.Red);
                SocketSendMsg(client, "Qequest Error! Demo: Get /api?utf=1&key=iQQBot.com&sendtype=SendFriendMessage", "Gb2312", key);
                return;
            }
            int iStartPos = str.IndexOf(" HTTP/", 1);
            if (iStartPos == -1)
            {
                ShowMessage("Receive An Error(3) Data...", ConsoleColor.Red);
                SocketSendMsg(client, "Qequest Error! Demo: Get /api?utf=1&key=iQQBot.com&sendtype=SendFriendMessage", "Gb2312", key);
                return;
            }

            // 获取GET参数
            Hashtable GetData = GetGetData(str.Substring(9, iStartPos - 9));

            // 默认编码
            string Bianma = "GB2312";
            if (GetData.ContainsKey("utf"))
            {
                if (GetData["utf"].ToString() == "1")
                {
                    Bianma = "UTF-8";
                }
            }
            GetData.Add("bianma", Bianma);

            string Data = string.Empty;
            try
            {
                Data = Api(GetData, Bianma);
            }
            catch
            {
                Data = "Request Data Error!";
            }
            SocketSendMsg(client, Data, Bianma, key);
        }

        /// <summary>
        /// 套接字: 发送数据
        /// </summary>
        /// <param name="client"></param>
        /// <param name="msg"></param>
        /// <param name="msgCharSet"></param>
        /// <param name="key"></param>
        /// <param name="mode"></param>
        private void SocketSendMsg(Socket client, string msg, string msgCharSet, string key, string mode = "UserMsg")
        {
            try
            {
                byte[] bytes = new byte[] { };

                // 如果消息为空就置错误
                if (msg.Trim() == string.Empty)
                {
                    msg = "Parameter Error";
                }

                // 默认资源类型
                string MIME = "";

                // 如果不是用户消息
                if (mode != "UserMsg")
                {
                    string path = string.Empty;
                    MIME = "image/png";

                    // 读取验证码图片
                    if (msg.Contains("Verify"))
                    {
                        path = Application.StartupPath + "\\QQ\\" + RobotConfig.QQNumber + "\\Verify.png";
                    }
                    // 群头像?
                    else if (msg.Contains("Cluster"))
                    {
                        path = Application.StartupPath + "\\QQ\\" + RobotConfig.QQNumber + "\\Cluster\\" + msg.Substring(8) + ".png";
                    }
                    // 好友头像?
                    else if (msg.Contains("Friend"))
                    {
                        path = Application.StartupPath + "\\QQ\\" + RobotConfig.QQNumber + "\\Friend\\" + msg.Substring(7) + ".jpg";
                    }
                    // 网站图标
                    else if (msg.Contains("Favicon"))
                    {
                        path = Application.StartupPath + "\\favicon.ico";
                        MIME = "image/x-icon";
                    }

                    // 读取文件
                    if(path != string.Empty)
                    {
                        bytes = ReadFile(path);
                    }

                    // 读取内容为空就设置错误
                    if (bytes == null)
                    {
                        bytes = Encoding.GetEncoding(msgCharSet).GetBytes("读取资源失败，请尝试重新获取！");
                        MIME = "";
                    }
                }
                else
                {
                    bytes = Encoding.GetEncoding(msgCharSet).GetBytes(msg);
                }

                // 发送消息
                SendHeader("", MIME, bytes.Length, client);
                client.Send(bytes, 0, bytes.Length, SocketFlags.None);
                client.Shutdown(SocketShutdown.Both);
                clients.Remove(key);
            }
            catch
            {

            }
            finally
            {
                clients.Remove(key);
            }
        }

        /// <summary>
        /// 读取文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public byte[] ReadFile(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.OpenOrCreate);
            byte[] buffer = new byte[fs.Length];
            try
            {
                fs.Read(buffer, 0, buffer.Length);
                fs.Seek(0, SeekOrigin.Begin);
                return buffer;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                }
            }
        }
        
        /// <summary>
        /// 发送头信息
        /// </summary>
        /// <param name="sHttpVersion"></param>
        /// <param name="sMIMEHeader"></param>
        /// <param name="iTotBytes"></param>
        /// <param name="client"></param>
        public void SendHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, Socket client)
        {

            String sBuffer = "";
            if (sMIMEHeader.Length == 0)
            {
                sMIMEHeader = "text/html"; // 默认 text/html 
            }
            if (sHttpVersion.Length == 0)
            {
                sHttpVersion = "HTTP/1.1";
            }
            sBuffer = sBuffer + sHttpVersion + " 200 OK\r\n";
            sBuffer = sBuffer + "Server: Z-Robot Robot Club Service\r\n";
            sBuffer = sBuffer + "Content-Type: " + sMIMEHeader + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            sBuffer = sBuffer + "Content-Length: " + iTotBytes + "\r\n\r\n";
            Byte[] bSendData = Encoding.UTF8.GetBytes(sBuffer);
            client.Send(bSendData, 0, bSendData.Length, SocketFlags.None);
        }

        /// <summary>
        /// 解析GET参数
        /// </summary>
        /// <param name="Data"></param>
        /// <returns></returns>
        public Hashtable GetGetData(string Data)
        {
            Hashtable GetData = new Hashtable();
            string[] data = Data.Split('&');
            for (int i = 0; i < data.Length; i++)
            {
                string[] dataArr = data[i].Split('=');
                if (dataArr.Length == 2)
                {
                    GetData[dataArr[0].ToLower()] = dataArr[1];
                }
            }
            return GetData;
        }

        /// <summary>
        /// 接口内容处理
        /// </summary>
        /// <param name="Data"></param>
        /// <param name="Bianma"></param>
        /// <returns></returns>
        public string Api(Hashtable Data, string Bianma)
        {
            // 判断密钥是否正确
            if (!Data.ContainsKey("key"))
            {
                return "Not Set key!";
            }
            if (Data["key"].ToString() != RobotConfig.RobotKey)
            {
                return "Key Error!";
            }

            // 判断是否提交方法
            if (!Data.ContainsKey("sendtype"))
            {
                return "Not Set sendtype!";
            }

            // 获取事件名
            string sendtype = Data["sendtype"].ToString().ToLower();

            // 调用方法事件
            return eventObj.CallAction(sendtype, Data);

            /*
            // 逐个判断请求类型
            string Ret = string.Empty;
            switch (Data["sendtype"].ToString().ToLower())
            {
                #region 发送普通消息
                case "sendmessage": //发送普通消息
                    {


                    }
                #endregion
                #region 发送临时会话消息
                case "sendtempsession"://发送普通消息
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : SendTempSessionMessage ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        int time = 200;
                        if (Data.Contains("time"))
                        {
                            try
                            {
                                time = int.Parse(Data["time"].ToString());
                            }
                            catch
                            {
                            }
                        }
                        SendTempSession(Data["id"].ToString(), HttpUtility.UrlDecode(Data["message"].ToString(), Encoding.GetEncoding(bianma)), time);
                        ShowMessage("Receive An Api : SendTempSessionMessage ...OK");
                        Ret = "参数提交成功!";
                        break;
                    }
                #endregion
                #region 发送群消息
                case "sendclustermessage":
                case "sendqunmessage"://发送群消息
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : SendClusterMessage ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        int time = 200;
                        if (Data.Contains("time"))
                        {
                            try
                            {
                                time = int.Parse(Data["time"].ToString());
                            }
                            catch
                            {
                            }
                        }
                        SendClusterMessage(Data["id"].ToString(), HttpUtility.UrlDecode(Data["message"].ToString(), Encoding.GetEncoding(bianma)), time);
                        ShowMessage("Receive An Api : SendClusterMessage ...OK");
                        Ret = "参数提交成功!";
                        break;
                    }
                #endregion
                #region 发送临时群消息
                case "sendtempclustermessage":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : SendTempClusterMessage ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        SendTempClusterMessage(uint.Parse(Data["id"].ToString()), HttpUtility.UrlDecode(Data["message"].ToString(), Encoding.GetEncoding(bianma)));
                        ShowMessage("Receive An Api : SendTempClusterMessage ...OK");
                        Ret = "参数提交成功!";
                    }
                    break;
                #endregion
                #region 建立临时会话消息通道
                case "createsession":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : CreatTempClusterMemberSession ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        CreateSession(Data["id"].ToString(), Data["message"].ToString());
                        ShowMessage("Receive An Api : CreatTempClusterMemberSession ...OK");
                        Ret = "参数提交成功!";
                    }
                    break;
                #endregion
                #region 发送窗口抖动
                case "sendvibration"://发送窗口抖动
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : SendVibration ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        SendVibration(Data["id"].ToString());
                        ShowMessage("Receive An Api : SendVibration ...OK");
                        Ret = "参数提交成功!";
                        break;
                    }
                #endregion
                #region 加群
                case "addcluster":
                case "addqun"://加群
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : AddCluster ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        AddCluster(uint.Parse(Data["id"].ToString()), HttpUtility.UrlDecode(Data["message"].ToString(), Encoding.GetEncoding(bianma)));
                        ShowMessage("Receive An Api : AddCluster ...OK");
                        Ret = "任务提交成功，请等待!";
                        break;
                    }
                #endregion
                #region 加好友
                case "addfriend":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : AddFriend ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        AddFriend(uint.Parse(Data["id"].ToString()), HttpUtility.UrlDecode(Data["message"].ToString(), Encoding.GetEncoding(bianma)));
                        ShowMessage("Receive An Api : AddFriend ...OK");
                        Ret = "任务提交成功，请等待!";
                        break;
                    }
                #endregion
                #region 退群
                case "exitcluster":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : ExitCluster ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }

                        ExitCluster(uint.Parse(Data["id"].ToString().Trim()));
                        ShowMessage("Receive An Api : ExitCluster ...OK");
                        Ret = "参数提交成功！";
                    }
                    break;
                #endregion
                #region T人
                case "kickoutmember":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : KickOutClusterMember ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        KickOutMember(Data["id"].ToString(), Data["message"].ToString());
                        ShowMessage("Receive An Api : KickOutClusterMember ...OK");
                        Ret = "参数提交成功！";
                    }
                    break;
                #endregion
                #region 邀请人
                case "invitemember":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : InviteClusterMember ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        InviteMember(Data["id"].ToString(), Data["message"].ToString());
                        ShowMessage("Receive An Api : InviteClusterMember ...OK");
                        Ret = "参数提交成功！";
                    }
                    break;
                #endregion
                #region 登陆/退出/重启机器人
                case "exitrobot"://退出机器人
                    {
                        ExitRobot();
                        ShowMessage("Receive An Api : ExitRobot ...OK");
                        Ret = "参数提交成功!";
                        break;
                    }
                case "loginrobot": //登陆机器人
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : LoginRobot ...Error");
                            Ret = "当前状态为登录成功，无需登录，如需重启，请执行重启命令。";
                            break;
                        }
                        LoginRobot();
                        ShowMessage("Receive An Api : LoginRobot ...OK");
                        Ret = "参数提交成功!";
                        break;
                    }
                case "resetrobot": //重启机器人
                    {
                        ResetRobot();
                        ShowMessage("Receive An Api : ResetRobot ...OK");
                        Ret = "参数提交成功!";
                        break;
                    }
                case "restartrobot": //重新开启机器人
                    {
                        ReStartRobot();
                        ShowMessage("Receive An Api : ReStartRobot ...OK");
                        Ret = "参数提交成功!";
                        break;
                    }
                #endregion
                #region 更改备注
                case "setfriendremark":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : SetFriendRemark ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        ShowMessage("Receive An Api : SetFriendRemark ...OK");
                        uint QQ = uint.Parse(Data["id"].ToString());
                        SetFriendRemark(QQ, HttpUtility.UrlDecode(Data["message"].ToString(), Encoding.GetEncoding(bianma)));
                        Ret = "参数提交成功！";
                        break;
                    }
                #endregion
                case "changeclustermembercard":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : ChangeClusterMemberCard ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        try
                        {
                            string ClusterNum = Data["id"].ToString();
                            string memberQQ = Data["memberqq"].ToString();
                            uint MemberQQ = 0;
                            if (memberQQ.Trim().ToLower() == "robot")
                            {
                                MemberQQ = RobotConfig.QQNumber;
                            }
                            else
                            { MemberQQ = uint.Parse(memberQQ); }
                            string CardName = HttpUtility.UrlDecode(Data["cardname"].ToString(), Encoding.GetEncoding(bianma));
                            //HttpUtility.UrlDecode(Data["cardname"].ToString(), Encoding.GetEncoding(bianma));
                            ShowMessage("Receive An Api : ChangeClusterMemberCard ...OK");
                            Sdk_ClusterInfo cluster = GetClusterInfo(uint.Parse(ClusterNum));
                            if (cluster != null)
                            {
                                if (cluster.MemberInfo[RobotConfig.QQNumber].IsAdmin | MemberQQ == RobotConfig.QQNumber)
                                {
                                    ChangeClusterMemberCard(cluster.ClusterNum, MemberQQ, CardName);
                                    Ret = "参数提交成功!";
                                }
                                else
                                {
                                    Ret = "不是本群管理员，只能更改自身名片";
                                }

                            }
                            else
                            {
                                Ret = "未找到该群号!";
                            }
                        }
                        catch
                        {
                            Ret = "参数错误";
                        }
                        break;
                    }
                case "changepassword": //更改QQ密码
                    {
                        ShowMessage("Receive An Api : ChangePassword ...OK");
                        RobotConfig.QQPassWord = Data["message"].ToString();
                        ShowMessage("ChangePassword to :" + RobotConfig.QQPassWord);
                        Ret = "密码更改成功！";
                        break;
                    }
                case "isclusteradmin": //是否为群管理
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : IsClusterAdmin ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        ShowMessage("Receive An Api : IsClusterAdmin ...OK");
                        Ret = IsClusterAdmin(uint.Parse(Data["id"].ToString()), uint.Parse(Data["message"].ToString())).ToString();
                        break;
                    }
                case "getclusterinfo": //获取群信息
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : GetClusterInfo ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        ShowMessage("Receive An Api : GetClusterInfo ...OK");
                        Sdk_ClusterInfo info = GetClusterInfo(uint.Parse(Data["id"].ToString().Trim()));
                        if (info != null)
                        {
                            try
                            {
                                string Nick = info.MemberInfo[info.Creator].Nick;
                                Ret = string.Format("群号：{0}\r人数：{1}\r群名称：{2}\r群主：{3}[{6}]\r群公告：{4}\r群描述：{5}", info.ClusterNum, info.NowNum, info.ClusterName, info.Creator, info.Notice, info.Description, Nick);//小变动
                            }
                            catch
                            {
                                Ret = string.Format("群号：{0}\r人数：{1}\r群名称：{2}\r群主：{3}\r群公告：{4}\r群描述：{5}", info.ClusterNum, info.NowNum, info.ClusterName, info.Creator, info.Notice, info.Description);//小变动
                            }
                        }
                        else
                        {
                            Ret = "抱歉，木有该群号。。";
                        }
                        break;
                    }
                case "modifysignature": //更改签名
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : ModifySignature ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        ModifySignature(HttpUtility.UrlDecode(Data["message"].ToString(), Encoding.GetEncoding(bianma)));
                        ShowMessage("Receive An Api : ModifySignature ...OK");
                        Ret = "参数提交成功!";
                        break;
                    }
                case "loginstatus": //返回当前登录状态
                    {
                        ShowMessage("Receive An Api : LoginStatus ...OK");
                        Ret = "当前状态为：" + GetLoginStatus();
                        break;
                    }
                case "updateconfig": //更新配置文件
                    {
                        UpdateConfig();
                        Ret = "更新成功";
                        break;
                    }
                case "updateinfo": //更新列表
                    {
                        UpdateRobotInfo();
                        Ret = "命令发送成功....";
                        break;
                    }
                case "getclustermember": //获取群某成员原始昵称+群昵称
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        Sdk_ClusterInfo info = GetClusterInfo(uint.Parse(Data["id"].ToString().Trim()));
                        if (info != null)
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (Sdk_MemberInfo item in info.MemberInfo.Values)
                            {
                                if (uint.Parse(Data["message"].ToString())==item.QQ) {
                                sb.Append(item.Nick+ "\n" +item.RemarksName + "\n" + item.Gender);
                                }
                            }
                            Ret = sb.ToString();
                        }
                        else
                        {
                            Ret = "木有找到该群号。";
                        }

                        break;
                    }
                case "getclustermemberinfo": //群成员发言时间
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        Sdk_ClusterInfo info = GetClusterInfo(uint.Parse(Data["id"].ToString().Trim()));
                        if (info != null)
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (Sdk_MemberInfo item in info.MemberInfo.Values)
                            {
                                sb.Append(item.QQ + "," + item.LastSpeak + "\n");
                            }
                            Ret = sb.ToString();
                        }
                        else
                        {
                            Ret = "木有找到该群号。";
                        }
                      break;
                    }
                case "getclustermembercard": //群成员名片
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        Sdk_ClusterInfo info = GetClusterInfo(uint.Parse(Data["id"].ToString().Trim()));
                        if (info != null)
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (Sdk_MemberInfo item in info.MemberInfo.Values)
                            {
                                sb.Append(item.QQ + "," + item.RemarksName + "\n");
                            }
                            Ret = sb.ToString();
                        }
                        else
                        {
                            Ret = "木有找到该群号。";
                        }
                        break;
                    }
                case "getclusterlist":
                case "getqunlist": //群列表
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        ShowMessage("Receive An Api : GetQunList ...OK");
                        foreach (Sdk_ClusterInfo clusterInfo in GetClusterList().Values)
                        {
                            Ret += string.Format("群号:{1}\r\n群主:{3}\r\n群人数:{0}\r\n群名称:{2}\r\n", clusterInfo.NowNum, clusterInfo.ClusterNum, clusterInfo.ClusterName, clusterInfo.Creator);
                        }
                        break;
                    }
                case "getfriendlist": //好友列表
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        ShowMessage("Receive An Api : GetFriendList ...OK");
                        foreach (Sdk_ContactInfo friend in GetFriendList().Values)
                        {
                            Ret += string.Format("QQ:{0} 昵称:{1}\r\n", friend.QQNumber, friend.Nick);
                        }
                        break;
                    }
                case "clearmemory": //清理内存
                    {
                        ShowMessage("Receive An Api : ClearMemory ...OK");
                        CleanMemory();
                        Ret = "内存清理完成！";
                        break;
                    }
                case "changestatus":
                case "status":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : ChangeStatus ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        string message = string.Empty;
                        if (Data.Contains("message"))
                        {
                            message = HttpUtility.UrlDecode(Data["message"].ToString(), Encoding.GetEncoding(bianma));
                        }
                        switch (Data["id"].ToString().ToLower())
                        {
                            case "online":
                                ChangeStatus(Data["id"].ToString().ToLower(), message);
                                Ret = "状态改变为：我在线上";
                                break;
                            case "away":
                                ChangeStatus(Data["id"].ToString().ToLower(), message);
                                Ret = "状态改变为：离开";
                                break;
                            case "busy":
                                ChangeStatus(Data["id"].ToString().ToLower(), message);
                                Ret = "状态改变为：忙碌";
                                break;
                            case "killme":
                                ChangeStatus(Data["id"].ToString().ToLower(), message);
                                Ret = "状态改变为：Q我吧";
                                break;
                            case "hidden":
                                ChangeStatus(Data["id"].ToString().ToLower(), message);
                                Ret = "状态改变为：隐身";
                                break;
                            case "quiet":
                                ChangeStatus(Data["id"].ToString().ToLower(), message);
                                Ret = "状态改变为：请勿打扰";
                                break;
                            default:
                                Ret = "参数错误，请使用(online|away|busy|killme|hidden|quiet)其一作为参数";
                                break;
                        }
                        break;
                    }
                case "changenick":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : ChangeNick ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        else
                        {
                            ChangeNick(HttpUtility.UrlDecode(Data["message"].ToString(), Encoding.GetEncoding(bianma)));
                            ShowMessage("Receive An Api : ChangeNick ...OK");
                            Ret = "参数传递成功！";
                        }
                        break;
                    }

                case "cluster":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : ChangeCluster ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        else
                        {
                            ShowMessage("Receive An Api : ChangeCluster ...OK");
                           Cluster(bool.Parse((Data["id"].ToString())));
                            Ret = "参数传递成功！";
                        }
                        break;
                    }
                case "getrobotclientkey":
                    {
                        if (GetLoginStatus().Contains("登录成功"))
                        {
                            ShowMessage("Receive An Api : GetRobotClientKey ...Error");
                            Ret = "尚未登录成功，当前状态为：" + GetLoginStatus();
                            break;
                        }
                        else
                        {
                            ShowMessage("Receive An Api : GetRobotClientKey ...OK");
                            Ret = GetRobotClientKey();
                        }
                        break;
                    }
                case "plug":
                    {
                        Ret = "未找到该插件，请确认已经安装并开启。";
                    }
                    break;
                default:
                    Ret = "Unknown Command";
                    break;

            }
            return Ret;
            */
        }

        /// <summary>
        /// 设置窗口
        /// </summary>
        public override void SetForm(){ }
        /// <summary>
        /// 插件名称
        /// </summary>
        public override String PluginName { get { return "API操作插件"; } }
        /// <summary>
        /// 插件唯一名称（英文+数字）
        /// </summary>
        public override String PluginKey { get { return "com.ImDong.ApiPlugins"; } }
        /// <summary>
        /// 作者
        /// </summary>
        public override string Author { get { return "ImDong"; } }
        /// <summary>
        /// 插件版本
        /// </summary>
        public override Version PluginVersion { get { return new Version(2018, 04, 18, 2058); } }
        /// <summary>
        /// 插件图片（暂时无效）
        /// </summary>
        public override Image PlugImage { get { return null; } }
        /// <summary>
        /// 插件说明
        /// </summary>
        public override string Description { get { return "青石"; } }
        /// <summary>
        /// 安装协议，不需要请留空
        /// </summary>
        public override string Agreement { get { return ""; } }
        /// <summary>
        /// 开发令牌(暂时无效)
        /// </summary>
        public override string DevelopmentToken { get { return ""; } }

    }
}
