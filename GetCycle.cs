using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ApiPlugin
{
    public class GetCycle
    {
        ApiPluginClass ApiPlugin = new ApiPluginClass();
        /// <summary>
        /// 初始化Get轮循
        /// </summary>
        /// <param name="robot"></param>
        public GetCycle(ApiPluginClass api)
        {
            ApiPlugin = api;
            Thread th = new Thread(new ThreadStart(CycleBody));
            th.Start();
            Echo("Get循环模式启动成功！");
        }
        #region 循环体
        /// <summary>
        /// 循环体
        /// </summary>
        public void CycleBody()
        {
            while (true)
            {
                try
                {
                    if (!string.IsNullOrEmpty(ApiPlugin.RobotConfig.InterfaceUrl) & ApiPlugin.RobotConfig.GetCycle)
                    {
                        if (!ApiPlugin.RobotConfig.IsKickOutGet)
                        {
                            try
                            {
                                if (ApiPlugin.RobotConfig.IsInSleepTime | !ApiPlugin.GetLoginStatus().Contains("成功"))
                                {
                                    Thread.Sleep(ApiPlugin.RobotConfig.Get_Time);
                                    continue;
                                }
                            }
                            catch
                            {
                                Thread.Sleep(ApiPlugin.RobotConfig.Get_Time);
                                continue;
                            }
                        }
                        Get();
                    }
                    Thread.Sleep(ApiPlugin.RobotConfig.Get_Time);
                }
                catch 
                {

                }
            }
        }
        #endregion
        /// <summary>
        /// Get方法执行函数
        /// </summary>
        public void Get()
        {
            CallInterFace api = new CallInterFace();
            string Url = ApiPlugin.RobotConfig.InterfaceUrl + "?Copyright=" + ApiPlugin.RobotConfig.RobotKey + "&RobotQQ=" + ApiPlugin.RobotConfig.QQNumber + "&Port=" + ApiPlugin.RobotConfig.ApiPort;
            string Result = api.GetData(Url, ApiPlugin.RobotConfig.InterfaceEncoder);
            Hashtable GetData = ApiPlugin.GetGetData(Result);
            string Bianma = "GB2312";
            if (GetData.ContainsKey("utf"))
            {
                if (GetData["utf"].ToString() == "1")
                {
                    Bianma = "UTF-8";
                }
            }
            string Data = string.Empty;
            try
            {
                Data = ApiPlugin.Api(GetData, Bianma);
            }
            catch
            {
                Data = "数据出错！请返回检查！";
            }
        }
        #region 输出方法
        /// <summary>
        /// 输出方法
        /// </summary>
        /// <param name="title"></param>
        /// <param name="p"></param>
        private void Echo(string p)
        {
            ApiPlugin.ShowMessage(p, ConsoleColor.Green);
        }
        #endregion
    }
}
