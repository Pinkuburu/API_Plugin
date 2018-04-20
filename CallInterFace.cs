using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;

namespace ApiPlugin
{
    public class CallInterFace
    {
        public CallInterFace()
        {
            ServicePointManager.Expect100Continue = false;
        }
        public String GetData(string url, string bianma)
        {
            try
            {
                string getcode;
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Credentials = CredentialCache.DefaultCredentials;
                request.Method = "GET";
                request.KeepAlive = true;
                request.ContentType = "application/x-www-form-urlencoded";
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                using (Stream resultStream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(resultStream, Encoding.GetEncoding(bianma)))
                    {
                        getcode = reader.ReadToEnd();
                    }
                }
                return getcode;
            }
            catch (Exception e)
            {
                return e.Message;
            }

        }
        public PostCallBack PostData(string url, string PostData, string bianma)
        {
            try
            {

                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Credentials = CredentialCache.DefaultCredentials;
                request.Method = "POST";
                request.KeepAlive = true;
                request.ContentType = "application/x-www-form-urlencoded";
                request.Timeout = 15000;
                byte[] bys = Encoding.Default.GetBytes(PostData);
                request.ContentLength = bys.Length;
                using (Stream inputStream = request.GetRequestStream())
                {
                    inputStream.Write(bys, 0, bys.Length);
                    inputStream.Close();
                }
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                PostCallBack call = new PostCallBack();
                using (Stream resultStream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(resultStream, Encoding.GetEncoding(bianma)))
                    {
                        call.Message = reader.ReadToEnd();
                    }
                }
                return call;
            }
            catch (Exception e)
            {
                PostCallBack call = new PostCallBack();
                call.Iserr = true;
                call.Message = e.Message;
                return call;
            }


        }
    }
    public class PostCallBack
    {
        public bool Iserr = false;
        public string Message = string.Empty;
    }
}
