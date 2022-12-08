using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;

namespace UsbApp
{
     /// <summary>
    /// NetHelper 。
    /// </summary>
    public class NetHelper
    {
        #region IsPublicIPAddress
        private Socket serSock;
        Socket clientSock = null;
        private Dictionary<IPEndPoint, Socket> clientSockList;
        public static bool IsPublicIPAddress(string ip)
        {
            if (ip.StartsWith("10.")) //A类 10.0.0.0到10.255.255.255.255 
            {
                return false;
            }

            if (ip.StartsWith("172."))//B类 172.16.0.0到172.31.255.255 
            {
                if (ip.Substring(6, 1) == ".")
                {
                    int secPart = int.Parse(ip.Substring(4, 2));
                    if ((16 <= secPart) && (secPart <= 31))
                    {
                        return false;
                    }
                }
            }

            if (ip.StartsWith("192.168."))//C类 192.168.0.0到192.168.255.255 
            {
                return false;
            }

            return true;
        }
        #endregion

        #region GetRemotingHanler
        //前提是已经注册了remoting通道
        public static object GetRemotingHanler(string channelTypeStr, string ip, int port, string remotingServiceName, Type destInterfaceType)
        {
            try
            {
                string remoteObjUri = string.Format("{0}://{1}:{2}/{3}", channelTypeStr, ip, port, remotingServiceName);
                return Activator.GetObject(destInterfaceType, remoteObjUri);
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region GetLocalIp
        /// <summary>
        /// GetLocalIp 获取本机的IP地址
        /// </summary>       
        public static IPAddress[] GetLocalIp()
        {
            string hostName = Dns.GetHostName();
            IPHostEntry hEntry = Dns.Resolve(hostName);

            return hEntry.AddressList;
        }

        public static IPAddress GetFirstLocalIp()
        {
            string hostName = Dns.GetHostName();
            IPHostEntry hEntry = Dns.Resolve(hostName);

            return hEntry.AddressList[0];
        }

        /// <summary>
        /// GetLocalPublicIp 获取本机的公网IP地址
        /// </summary>       
        public static string GetLocalPublicIp()
        {
            IPAddress[] list = GetLocalIp();
            foreach (IPAddress ip in list)
            {
                if (IsPublicIPAddress(ip.ToString()))
                {
                    return ip.ToString();
                }
            }

            return null;
        }
        #endregion

        #region IsConnectedToInternet
        /// <summary>
        /// IsConnectedToInternet 机器是否联网
        /// </summary>       
        public static bool IsConnectedToInternet()
        {
            int Desc = 0;
            return InternetGetConnectedState(Desc, 0);
        }

        [DllImport("wininet.dll")]
        private extern static bool InternetGetConnectedState(int Description, int ReservedValue);
        #endregion
        public static string GetIP()
        {
            string tempip = "";
            try
            {
                WebRequest wr = WebRequest.Create("http://www.ip138.com/ips138.asp");
                Stream s = wr.GetResponse().GetResponseStream();
                StreamReader sr = new StreamReader(s, Encoding.Default);
                string all = sr.ReadToEnd(); //读取网站的数据

                int start = all.IndexOf("您的IP地址是：[") + 9;
                int end = all.IndexOf("]", start);
                tempip = all.Substring(start, end - start);
                sr.Close();
                s.Close();
            }
            catch
            {
            }
            return tempip;
        }
        public  void SocketStart()
        {
            int port = 8808;
            serSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EndPoint ep = new IPEndPoint(IPAddress.Any, port);
            serSock.Bind(ep);
            serSock.Listen(30);
            clientSockList = new Dictionary<IPEndPoint, Socket>();
            serSock.BeginAccept(new AsyncCallback(AcceptEnd), null);
        }
        /// <summary>
        /// 接收客户机连接的方法
        /// </summary>
        /// <param name="ar"></param>
        private void AcceptEnd(IAsyncResult ar)
        {
            Socket sock = serSock.EndAccept(ar);
            IPEndPoint ep = sock.RemoteEndPoint as IPEndPoint;
            clientSockList[ep] = sock;
            //  btnSend.Enabled = true;
            //  btnSendFile.Enabled = true;
            //  uList.Items.Add(sock.RemoteEndPoint);
            Thread t = new Thread(new ParameterizedThreadStart(RecThread));
            t.IsBackground = true;
            t.Start(sock);
            serSock.BeginAccept(new AsyncCallback(AcceptEnd), null); //继续接收下一个客户机连接
        }
        private void RecThread(object o)
        {
            clientSock = o as Socket;
            NetworkStream ns = new NetworkStream(clientSock);
            do
            {
                try
                {
                   byte[] buffer = new byte[1024];
                    //int bytesRead = ns.Read(buffer, 0, BufferSize);
                    //DataType dt = new DataType();
                    //int type = dt.toDataType(buffer, bytesRead);
                    //if (type == 0)
                    //{
                    //    // this.parse_Bytes(buffer, bytesRead);
                    //    this.textBox1.Text += System.Text.Encoding.ASCII.GetString(buffer, 0, buffer.Length);
                    //}
                    //else
                    //{
                    //    this.InptuText(buffer, bytesRead);
                    //    this.SendBack(clientSock, type);
                    //}

                    Array.Clear(buffer, 0, buffer.Length);
                }
                catch
                {
                    // MessageBox.Show("断开连接");
                }
            }
            while (true);

        }
        // Analytical data then call socket 
        private void SendBack(Socket socket, int msg)
        {
            //byte[] answer;        
            //answer = Encoding.ASCII.GetBytes("@VER," + this.textBox5.Text.Trim() + "," + pagenum + "#");
            //this.Send(socket, answer);          
        }
        //for socket, send back the Packages Data
        public void Send(Socket socket, byte[] data)
        {
            if (data.Length > 0)
            {
                socket.BeginSend(data, 0, data.Length, 0,
                new AsyncCallback(this.SendCallback), socket);
            }
        }

        protected void SendCallback(IAsyncResult ar)
        {
            Socket handler = (Socket)ar.AsyncState;
            try
            {
                int bytesSent = handler.EndSend(ar);

            }
            catch (Exception e)
            {
            }
        }
    }
}
