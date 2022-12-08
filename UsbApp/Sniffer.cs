using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using UsbLibrary;
using System.IO;
using System.Threading;
using System.Xml;
using System.Net;
using System.Net.Sockets;

namespace UsbApp
{
    public partial class Sniffer : Form
    {
        // private byte[] FrimwareBinArray;


        List<byte[]> FrimwareBinArrayList = new List<byte[]>();
        List<byte[]> FrimwareBinArralyListTemp = new List<byte[]>();
        List<byte[]> ConfigArrayList = new List<byte[]>();
        static List<byte[]> FrimwareBinOTAArrayList = new List<byte[]>();
        List<byte[]> FrimwareBinOTAArralyListTemp = new List<byte[]>();

        byte[] ConfigArray = new byte[326];

        StringBuilder DisplayStringBuilder = new StringBuilder();
        byte PageHead = 0x40;
        int ASKConfigFlag = 1;
        string LoadConfigFromTeStringTemp = string.Empty;
        delegate void SetTextCallback(string text);

        delegate void SetGPRSTextCallback(string text);

        static AsyncTcpServer server;

        public delegate void UpdateControlEventHandler(Object sender, EventArgs e);
        public static event UpdateControlEventHandler UpdateControl;
        static TcpClient NowTcpClient = null;
        static String RecTempString = string.Empty;
        public Sniffer()
        {
            InitializeComponent();
            System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = false;
            UpdateControl += new UpdateControlEventHandler(this.Test);
            string ip = NetHelper.GetIP();
            if (ip != "")
            {
                this.Text = "Configuration Manager (Version 9_1.0)(" + ip + ")";
                this.UnitTestIptb.Text = ip;
            }
        }

        public void Test(Object o, EventArgs e)  //事件处理函数，用来更新控件
        {
            try
            {
                string ss = o as string;
                switch (ss.Substring(0, 1))
                {
                    case "C":
                        this.Connectiontb.Text = ss.Substring(1);
                        this.OTAbt.Enabled = true;
                        break;
                    case "D":
                        this.Connectiontb.Text = "";
                        this.OTAbt.Enabled = true;
                        break;
                    case "L":
                        ss = ss.Substring(1);
                        SettbForeColorBlack();
                        LoadConfigFromTeStringTemp = string.Empty;
                      //  DisplayGPRSLog(ss.Replace("\0", "*") + "\r\n\r\n");
                        for (int i = 0; i < 6; i++)
                        {
                            try
                            {                            
                                if (ss.Length >= 58)
                                {
                                    LoadConfig(i+ 1, ss.Substring(0, 58));
                                    ss = ss.Substring(58);
                                }
                                else
                                {
                                    LoadConfig(i + 1, ss);
                                }
                            }
                            catch
                            {

                            }
                        }
                        this.OTAbt.Enabled = true;
                        MessageBox.Show("GPRS load config success");
                        break;
                    case "O":
                        this.OTAbt.Enabled = false;
                        int vPageNum = Convert.ToInt32(ss.Substring(1));
                        this.GPRSpb.Value = 100 * vPageNum / FrimwareBinOTAArrayList.Count;
                        this.OTAlb.Text = (100 * vPageNum / FrimwareBinOTAArrayList.Count).ToString() + "%";
                        break;
                    case "E":
                        MessageBox.Show("Config success"); 
                        break;
                    default:
                        DisplayGPRSLog(ss + "\r\n\r\n");
                        this.OTAbt.Enabled = true;
                        break;
                }
            }
            catch
            {
            }
        }

        private void usb_OnDeviceArrived(object sender, EventArgs e)
        {
            this.toolStripStatusLabel2.ForeColor = Color.Green;
            this.toolStripStatusLabel2.Text = "Connected";
        }

        private void usb_OnDeviceRemoved(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler(usb_OnDeviceRemoved), new object[] { sender, e });
            }
            else
            {
                this.toolStripStatusLabel2.ForeColor = Color.Red;
                this.toolStripStatusLabel2.Text = "Disconnected";
            }
        }

        private void usb_OnSpecifiedDeviceArrived(object sender, EventArgs e)
        {
            this.toolStripStatusLabel2.ForeColor = Color.Green;
            this.toolStripStatusLabel2.Text = "Connected";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            usb.RegisterHandle(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            try
            {
                usb.ParseMessages(ref m);
                base.WndProc(ref m);	// pass message on to base form
            }
            catch (Exception ex)
            {

            }
        }

        private void usb_OnSpecifiedDeviceRemoved(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler(usb_OnSpecifiedDeviceRemoved), new object[] { sender, e });
            }
            else
            {
                // this.lb_message.Items.Add("My device was removed");
                this.toolStripStatusLabel2.ForeColor = Color.Red;
                this.toolStripStatusLabel2.Text = "Disconnected";
            }
        }

        private void usb_OnDataRecieved(object sender, DataRecievedEventArgs args)
        {
            if (InvokeRequired)
            {
                try
                {
                    Invoke(new DataRecievedEventHandler(usb_OnDataRecieved), new object[] { sender, args });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            else
            {
                string vRecText = string.Empty;
                if (args.data[0] == 0x40)
                {
                    switch (Convert.ToInt32(args.data[2]))
                    {
                        case 11://HID固件升级0b

                            int vPageNumber = Convert.ToInt32(args.data[3] << 8) + Convert.ToInt32(args.data[4]);
                            if (vPageNumber > 0 && vPageNumber < 65535 && FrimwareBinArrayList.Count > 0)
                            {
                                this.Updatingpb.PerformStep();
                                this.ProgressBarlb.Text = (100 * vPageNumber / FrimwareBinArrayList.Count).ToString() + "%";
                                USBDataSend(FrimwareBinArrayList[vPageNumber - 1]);
                                if (vPageNumber >= FrimwareBinArrayList.Count - 1)
                                {
                                    this.LoadBinbt.Enabled = true;
                                }
                            }
                            break;
                        case 175://Af指令数据下发回复
                            //DisplayLog("Rec Af:");
                            //DisplayLog(args.data[0].ToString("X2") + " ");//包头
                            //DisplayLog(args.data[1].ToString("X2") + " ");//指令类型
                            //DisplayLog( args.data[2].ToString("X2") + " ");//指令类型
                            //DisplayLog( Encoding.ASCII.GetString(args.data).Substring(3, 58).Replace("\0", ""));
                            //DisplayLog("\r\n");    
                            break;
                        case 173://配置数据下发发回复ad  
                            ASKConfigFlag++;
                            DisplayLog("Save Config:");
                            DisplayLog(Convert.ToInt32(args.data[3] << 8) + Convert.ToInt32(args.data[4]).ToString());
                            DisplayLog("\r\n");
                            break;
                        case 220:                    
                            LoadConfigFromTe(args.data);
                            break;
                        case 218://调试信息da
                            DisplayStringBuilder.Append(Encoding.ASCII.GetString(args.data).Substring(4, Convert.ToInt32(args.data[1])));
                            if (args.data[3] == 0xff)
                            {
                                DisplayDataParsing(DisplayStringBuilder.ToString());
                                DisplayLog("\r\n");
                                DisplayStringBuilder.Length = 0;
                            }
                            break;
                        default:
                            break;
                    }
                }


            }
        }
        private void DisplayDataParsing(string pDisplayData)
        {
            switch (pDisplayData.Substring(0, 4))
            {
                case "A001":
                    pDisplayData = "N0 SOS CenterNumber \r\n";
                    break;
                case "A002":
                    pDisplayData = "SOS\r\n";
                    break;
                case "A005":
                    pDisplayData = "Phone number error\r\n";
                    break;
                case "A006":
                    pDisplayData = "Ip or port error\r\n";
                    break;
                case "A007":
                    pDisplayData = "System Restart\r\n";
                    break;
                case "A008":
                    pDisplayData = "GSM Power on\r\n";
                    break;
                case "A009":
                    pDisplayData = "GSM Power off\r\n";
                    break;
                case "A010":
                    pDisplayData = "GPRS Close\r\n";
                    break;
                case "A011":
                    pDisplayData = "GPRS Connect ok\r\n";
                    break;
                case "A012":
                    pDisplayData = "GPRS Connect fail\r\n";
                    break;
                case "A003":
                    pDisplayData = "Install Settings\r\n";
                    break;
                case "A013":
                    pDisplayData = "Set success\r\n";
                    break;
                case "A014":
                    pDisplayData = "Password error\r\n";
                    break;
                case "A015":
                    pDisplayData = "Instruction format error\r\n";
                    break;
                case "A004":
                    pDisplayData = "Restore factory Settings\r\n";
                    break;
                case "A017":
                    pDisplayData = "No gps satellite\r\n";
                    break;
                case "A018":
                    pDisplayData = "No gps data\r\n";
                    break;
                case "A016":
                    pDisplayData = "Wipe cache\r\n";
                    break;
                case "A019":
                    pDisplayData = "Instruction";
                    break;
                case "B016":
                    pDisplayData = "Instruction:" + pDisplayData.Substring(4);
                    break;
                case "B017":
                    pDisplayData = "SMS Instruction:" + pDisplayData.Substring(4);
                    break;
                case "B018":
                    pDisplayData = "GPRS Instruction:" + pDisplayData.Substring(4);
                    break;
                case "B019":
                    if (pDisplayData.Substring(4, 5) == "*997#")
                    {

                    }
                    pDisplayData = "Set success:" + pDisplayData.Substring(4);

                    break;
                case "B020":
                    pDisplayData = "In Queue:" + pDisplayData.Substring(4);
                    break;
                case "B021":
                    pDisplayData = pDisplayData.Substring(4);
                    pDisplayData = "Get Settings" + pDisplayData;
                    break;
                case "B023":
                    pDisplayData = "SMS Number:" + pDisplayData.Substring(4);
                    break;
                case "B022":
                    pDisplayData = "SMS Data:" + pDisplayData.Substring(4);
                    break;
                case "B004":
                    pDisplayData = "Set DNS:" + pDisplayData.Substring(4);
                    break;
                case "B005":
                    pDisplayData = "Set APN:" + pDisplayData.Substring(4);
                    break;
                case "B006":
                    pDisplayData = "GPRS Connect:" + pDisplayData.Substring(4);
                    break;
                case "B007":
                    pDisplayData = "Send SMS:" + pDisplayData.Substring(4);
                    break;
                case "B008":
                    pDisplayData = "IMEI:" + pDisplayData.Substring(4);
                    break;
                case "B009":
                    pDisplayData = "Sending Data:" + pDisplayData.Substring(4);
                    break;
                case "B010":
                    pDisplayData = "Set Time:" + pDisplayData.Substring(4);
                    break;
                case "B011":
                    pDisplayData = "Received a call:" + pDisplayData.Substring(4);
                    break;
                case "B012":
                    pDisplayData = "Get through:" + pDisplayData.Substring(4);
                    break;
                case "B013":
                    pDisplayData = "In the call:" + pDisplayData.Substring(4);
                    break;
                case "B014":
                    pDisplayData = "Hang up the phone:" + pDisplayData.Substring(4);
                    break;
                case "B015":
                    pDisplayData = "Receive a sms message:" + pDisplayData.Substring(4);
                    break;
                case "B002":
                    pDisplayData = "Receive a gprs message:" + pDisplayData.Substring(4);
                    break;
                case "B003":
                    pDisplayData = "Version:" + pDisplayData.Substring(4);
                    break;
                case "C001":
                    break;
                case "D001":

                    break;
                case "E001":
                    pDisplayData = pDisplayData.Substring(4);
                    break;
                case "B001":
                    DisplayDataParsingLog(pDisplayData.Substring(4));
                    pDisplayData = "";
                    break;
                default:
                    break;
            }
            DisplayLog(pDisplayData);
        }
        private void DisplayDataParsingLog(string pLogData)
        {
            try
            {
                this.toolStripStatusLabel14.Text = pLogData.Substring(0, 15).ToString();
                DisplayLog("Time:" + pLogData.Substring(15, 12).ToString() + "\r\n");
                DisplayLog("IOStatus" + pLogData.Substring(27, 6).ToString() + "\r\n");
                DisplayLog("Battery voltage:" + pLogData.Substring(34, 1).ToString() + "." + pLogData.Substring(35, 1) + "   External voltage:" + pLogData.Substring(36, 2) + "\r\n");
                DisplayLog("AN1:" + pLogData.Substring(38, 2) + "." + pLogData.Substring(40, 2) + "\r\n");
                DisplayLog("LACCI:" + pLogData.Substring(46, 4) + "   GPS Status:" + pLogData.Substring(50, 1) + "   GPS Fix:" + pLogData.Substring(51, 2) + "\r\n");
                DisplayLog("Angel:" + pLogData.Substring(53, 3) + "   Speed:" + pLogData.Substring(56, 3) + "\r\n");
                DisplayLog("Latitude:" + pLogData.Substring(59, 10) + " " + pLogData.Substring(69, 1) + "   Longitude:" + pLogData.Substring(70, 11) + " " + pLogData.Substring(81, 1) + "\r\n");

            }
            catch (System.Exception ex)
            {
                //   DisplayLog("Error Log:" + pLogData);
                // MessageBox.Show("数据出错" + ex.Message);
            }

        }
        private void DisplayLog(string pDislay)
        {
            if (this.ShowLogCKb.Checked)
            {
                if (this.LogTb.InvokeRequired)
                {
                    SetTextCallback d = new SetTextCallback(DisplayLog);
                    this.Invoke(d, new object[] { pDislay });
                }
                else
                {
                    this.LogTb.AppendText(pDislay);
                }
            }
        }
        private void SaveLog1()
        {
            string vFileName = AppDomain.CurrentDomain.BaseDirectory + "Log\\";
            if (!Directory.Exists(vFileName))
            {
                Directory.CreateDirectory(vFileName);
            }
            vFileName += DateTime.Now.ToString("yyyyMMddhhmmss") + "Log.txt";
            StreamWriter vStreamWriter = new StreamWriter(vFileName, true);
            vStreamWriter.Write(this.LogTb.Text.ToString());
            vStreamWriter.Close();
            this.LogTb.Text = "";
        }
        private void LoadConfigFromTe(byte[] pConfigData)
        {
            try
            {

                int vConfigPageNumber = Convert.ToInt32(pConfigData[3] << 8) + Convert.ToInt32(pConfigData[4]);
                byte[] vAcData = new byte[64];
                vAcData[0] = PageHead;
                vAcData[1] = 0x04;
                vAcData[2] = 0xAC;
                vAcData[3] = pConfigData[3];
                vAcData[4] = pConfigData[4];
                vAcData[63] = CheckSum(vAcData);
                LoadConfig(vConfigPageNumber, Encoding.ASCII.GetString(pConfigData).Substring(5, 58));
                USBDataSend(vAcData);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }
        private void LoadConfig(int vConfigPageNumber, string pLoadConfigFromTeStringTemp)
        {
            LoadConfigFromTeStringTemp += pLoadConfigFromTeStringTemp;
            try
            {

                if (vConfigPageNumber <= 6)
                {
                    switch (vConfigPageNumber)
                    {
                        case 1: //58           
                            this.Vehicletb.Text = LoadConfigFromTeStringTemp.Substring(0, 17).Replace("\0", "");
                            this.SIMPINtb.Text = LoadConfigFromTeStringTemp.Substring(17, 4).Replace("\0", "");
                            this.GsmBandcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(21, 1));
                            this.APNtb.Text = LoadConfigFromTeStringTemp.Substring(22, 30).Replace("\0", "");
                            LoadConfigFromTeStringTemp = LoadConfigFromTeStringTemp.Substring(52, 6);
                            break;
                        case 2://64  
                            this.APNUserNametb.Text = LoadConfigFromTeStringTemp.Substring(0, 20).Replace("\0", "");
                            this.APNPasswordtb.Text = LoadConfigFromTeStringTemp.Substring(20, 20).Replace("\0", "");
                            this.DomailIpcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(40, 1));
                            LoadConfigFromTeStringTemp = LoadConfigFromTeStringTemp.Substring(41, 23);
                            break;
                        case 3://81
                            int length = LoadConfigFromTeStringTemp.Length;
                            this.ServerDomainIptb.Text = LoadConfigFromTeStringTemp.Substring(0, 30).Replace("\0", "");
                            this.ServerPorttb.Text = LoadConfigFromTeStringTemp.Substring(30, 5).Replace("\0", "");
                            this.ServerProtocolcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(35, 1));
                            this.ServerResporiseTimeoutud.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(36, 4).Replace("\0", ""));
                            this.Heartbeatud.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(40, 4).Replace("\0", ""));
                            this.MovementDetectionIgnitioncb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(44, 1));
                            this.MovementDetection3Dcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(45, 1));
                            this.MovementDetectionDelayud.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(46, 4).Replace("\0", ""));

                            this.MovingTimeud1.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(50, 5).Replace("\0", ""));
                            this.MovingDistanceud1.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(55, 5).Replace("\0", ""));
                            this.MovingAngleud1.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(60, 3).Replace("\0", ""));
                            this.StopTimeud1.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(63, 5).Replace("\0", ""));
                            this.Sleepcb1.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(68, 1));
                            this.SleepDelayud1.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(69, 4).Replace("\0", ""));
                            this.OverSpeedcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(73, 1));
                            this.OverSpeedud.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(74, 3).Replace("\0", ""));
                            LoadConfigFromTeStringTemp = LoadConfigFromTeStringTemp.Substring(77, 4);
                            break;
                        case 4://62
                            this.Autoansercb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(0, 1));
                            this.Authorizedphone1tb.Text = LoadConfigFromTeStringTemp.Substring(1, 20).Replace("\0", "");
                            this.Authorizedphone2tb.Text = LoadConfigFromTeStringTemp.Substring(21, 20).Replace("\0", "");
                            this.Authorizedphone3tb.Text = LoadConfigFromTeStringTemp.Substring(41, 20).Replace("\0", "");
                            LoadConfigFromTeStringTemp = LoadConfigFromTeStringTemp.Substring(61, 1);
                            break;
                        case 5://59                
                            this.AD2IN1cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(0, 1));
                            this.IN2cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(1, 1));
                            this.IN3cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(2, 1));
                            this.IN4cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(3, 1));
                            this.IN5cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(4, 1));
                            this.Output1cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(5, 1));
                            this.Output2cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(6, 1));
                            this.Output3cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(7, 1));
                            this.Output4cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(8, 1));

                            this.AD1cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(9, 1));
                            this.AD1Lowlevelnd.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(10, 5).Replace("\0", ""));
                            this.AD1Highlevelnd.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(15, 5).Replace("\0", ""));

                             this.AD2cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(20, 1));
                            this.AD2Lowlevelnd.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(21, 5).Replace("\0", ""));
                            this.AD2Highlevelnd.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(26, 5).Replace("\0", ""));

                             this.AD3cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(31, 1));
                            this.AD3Lowlevelnd.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(32, 5).Replace("\0", ""));
                            this.AD3Highlevelnd.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(37, 5).Replace("\0", ""));

                             this.AD4cb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(42, 1));
                            this.AD4Lowlevelnd.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(43, 5).Replace("\0", ""));
                            this.AD4Highlevelnd.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(48, 5).Replace("\0", ""));

                            this.GeoEntereventcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(53, 1));
                            this.GeoExiteventcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(54, 1));
                            this.GeoRadiusud.Value = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(55, 4).Replace("\0", ""));  
                            LoadConfigFromTeStringTemp = "";
                            break;
                        case 6://58     
                            this.GeoLat1tb.Text = LoadConfigFromTeStringTemp.Substring(0, 10).Replace("\0", "");
                            this.GeoLong1tb.Text = LoadConfigFromTeStringTemp.Substring(10, 11).Replace("\0", "");
                            this.Gpsantennacutcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(21, 1));
                            this.Externalpowercutcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(22, 1));
                            this.Towcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(23, 1));
                            this.SOScb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(24, 1));
                            this.GEOINcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(25, 1));
                            this.GEOOUTcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(26, 1));
                            this.IN1ONcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(27, 1));
                            this.IN1OFFcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(28, 1));
                            this.IN2ONcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(29, 1));
                            this.IN2OFFcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(30, 1));
                            this.IN3ONcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(31, 1));
                            this.IN3OFFcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(32, 1));
                            this.IN4ONcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(33, 1));
                            this.IN5ONcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(34, 1));
                            this.IN5OFFcb.SelectedIndex = Convert.ToInt32(LoadConfigFromTeStringTemp.Substring(35, 1));
                            this.SMSPhonetb.Text = LoadConfigFromTeStringTemp.Substring(36, 20).Replace("\0", "");
                            LoadConfigFromTeStringTemp =LoadConfigFromTeStringTemp.Substring(56, 1);;
                            break;
                        default:
                            break;
                    }
                }
            }
            catch
            {
                MessageBox.Show("格式不对");
            }
        }
        private void usb_OnDataSend(object sender, EventArgs e)
        {
            //   this.lb_message.Items.Add("Some data was send");
        }

        private void button3_Click(object sender, EventArgs e)
        {//发送数据
            SendData(this.tb_send.Text.Trim(), 0xCF);
        }

        private bool SendData(string pStr, byte pCommandType)
        {
            if (this.Passwordtb.Text.Length == 6)
            {
                try
                {
                    byte[] vSendCmdPage = new byte[64];
                    string xx = this.Passwordtb.Text.Trim().ToString() + "," + pStr;
                    byte[] vSendCmdByte = Encoding.ASCII.GetBytes(xx);

                    vSendCmdPage[0] = PageHead;
                    vSendCmdPage[2] = pCommandType;
                    int vSendCmdByteHave = vSendCmdByte.Length;
                    int vSendCmdByteReadIndex = 0;
                    vSendCmdPage[1] = (byte)Convert.ToByte(vSendCmdByteHave);//数据长度
                    for (int i = 0; i < 60; i++)
                    {
                        if (vSendCmdByteHave - i > 0)
                        {
                            vSendCmdPage[i + 3] = vSendCmdByte[vSendCmdByteReadIndex];
                            vSendCmdByteReadIndex += 1;
                        }
                        else
                        {
                            vSendCmdPage[i + 3] = 0x00;//数据不够60个byte部分补0
                        }
                    }
                    vSendCmdPage[63] = CheckSum(vSendCmdPage);//Checksum
                    USBDataSend(vSendCmdPage);
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    return false;
                }
            }
            else
            {
                MessageBox.Show("Please enter the password");
                return false;
            }
        }
        private bool USBDataSend(byte[] pDataByte)
        {
            try
            {
                if (this.usb.SpecifiedDevice != null)
                {
                    this.usb.SpecifiedDevice.SendData(pDataByte);
                    return true;
                }
                else
                {
                    MessageBox.Show("Sorry but your device is not present. Plug it in!! ");
                    return false;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return false;
            }
        }
        private void ClearLogbt_Click(object sender, EventArgs e)
        {//清理LOG文本
            this.LogTb.Text = "";

        }

        private void LoadBinbt_Click(object sender, EventArgs e)
        {
            LoadedBinofdg.Filter = "BIN files(*.bin)|*.bin";
            LoadedBinofdg.FileName = "Frimware";
            if (LoadedBinofdg.ShowDialog() == DialogResult.OK)
            {
                FrimwareBinArrayList.Clear();
                string filepath = LoadedBinofdg.FileName;
                string filename = LoadedBinofdg.SafeFileName;
                this.BinNametb.Text = filename;
                FileStream vBinFileStream;
                vBinFileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read);
                int vBinSize = (int)vBinFileStream.Length;
                byte[] vFrimwareBinArray = new byte[vBinSize];
                vBinFileStream.Read(vFrimwareBinArray, 0, vBinSize);
                int vBinSizeReadIndex = 0;
                int vBinSizeHave = 0;
                do
                {
                    vBinSizeHave = vBinSize - vBinSizeReadIndex;
                    byte[] vFrimewareBinArrayTemp = new byte[64];
                    vFrimewareBinArrayTemp[0] = PageHead;
                    vFrimewareBinArrayTemp[2] = 0x0B;
                    if (vBinSizeHave > 58)
                    {
                        vFrimewareBinArrayTemp[1] = 0x3A;//数据长度                      
                        vFrimewareBinArrayTemp[3] = (byte)((0xff00 & FrimwareBinArrayList.Count + 1) >> 8);
                        vFrimewareBinArrayTemp[4] = (byte)(0xff & FrimwareBinArrayList.Count + 1);
                        for (int i = 0; i < 58; i++)
                        {
                            vFrimewareBinArrayTemp[i + 5] = vFrimwareBinArray[vBinSizeReadIndex];
                            vBinSizeReadIndex += 1;
                        }
                        vFrimewareBinArrayTemp[63] = CheckSum(vFrimewareBinArrayTemp);//Checksum
                        FrimwareBinArrayList.Add(vFrimewareBinArrayTemp);
                    }
                    else if (vBinSizeHave > 0)
                    {
                        vFrimewareBinArrayTemp[3] = (byte)((0xff00 & FrimwareBinArrayList.Count + 1) >> 8);
                        vFrimewareBinArrayTemp[4] = (byte)(0xff & FrimwareBinArrayList.Count + 1);
                        vFrimewareBinArrayTemp[1] = (byte)Convert.ToByte(vBinSizeHave);//数据长度
                        vFrimewareBinArrayTemp[3] = 0xFF;
                        vFrimewareBinArrayTemp[4] = 0xFF;
                        for (int i = 0; i < 58; i++)
                        {
                            if (vBinSizeHave - i > 0)
                            {
                                vFrimewareBinArrayTemp[i + 5] = vFrimwareBinArray[vBinSizeReadIndex];
                                vBinSizeReadIndex += 1;
                            }
                            else
                            {
                                vFrimewareBinArrayTemp[i + 5] = 0x00;//数据不够60个byte部分补0
                            }
                        }
                        vFrimewareBinArrayTemp[63] = CheckSum(vFrimewareBinArrayTemp);//Checksum
                        FrimwareBinArrayList.Add(vFrimewareBinArrayTemp);
                    }

                } while (vBinSizeHave > 0);
                FrimwareBinArrayList[FrimwareBinArrayList.Count - 1][3] = 0xff;
                FrimwareBinArrayList[FrimwareBinArrayList.Count - 1][4] = 0xff;
                vBinFileStream.Close();
                this.Updatingpb.Minimum = 0;
                this.Updatingpb.Maximum = FrimwareBinArrayList.Count;
                this.Updatingpb.Step = 1;
                this.UpdateFirmwarebt.Enabled = true;
                GetOTABin(filepath);
            }
        }

        private void GetOTABin(string filepath)
        {
            try
            {
                FrimwareBinOTAArrayList.Clear();
                FileStream vBinFileStream;
                vBinFileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read);
                int vBinSize = (int)vBinFileStream.Length;
                byte[] vFrimwareBinArray = new byte[vBinSize];
                vBinFileStream.Read(vFrimwareBinArray, 0, vBinSize);
                int vBinSizeReadIndex = 0;
                int vBinSizeHave = 0;
                do
                {
                    vBinSizeHave = vBinSize - vBinSizeReadIndex;

                    if (vBinSizeHave > 512)
                    {
                        byte[] vFrimewareBinArrayTemp = new byte[526];

                        string xx = "$$0526C0" + (FrimwareBinOTAArrayList.Count + 1).ToString("D4");
                        byte[] vArrByte = System.Text.Encoding.ASCII.GetBytes(xx);

                        vFrimewareBinArrayTemp[0] = vArrByte[0];
                        vFrimewareBinArrayTemp[1] = vArrByte[1];

                        vFrimewareBinArrayTemp[2] = vArrByte[2];
                        vFrimewareBinArrayTemp[3] = vArrByte[3];
                        vFrimewareBinArrayTemp[4] = vArrByte[4];
                        vFrimewareBinArrayTemp[5] = vArrByte[5];
                        vFrimewareBinArrayTemp[6] = vArrByte[6];
                        vFrimewareBinArrayTemp[7] = vArrByte[7];

                        vFrimewareBinArrayTemp[8] = vArrByte[8];
                        vFrimewareBinArrayTemp[9] = vArrByte[9];
                        vFrimewareBinArrayTemp[10] = vArrByte[10];
                        vFrimewareBinArrayTemp[11] = vArrByte[11];

                        for (int i = 0; i < 512; i++)
                        {
                            vFrimewareBinArrayTemp[i + 12] = vFrimwareBinArray[vBinSizeReadIndex];
                            vBinSizeReadIndex += 1;
                        }
                        byte[] arrByte = System.Text.Encoding.ASCII.GetBytes(CheckSumString(vFrimewareBinArrayTemp, 524));
                        vFrimewareBinArrayTemp[524] = arrByte[0];//Checksum
                        vFrimewareBinArrayTemp[525] = arrByte[1];//Checksum
                        FrimwareBinOTAArrayList.Add(vFrimewareBinArrayTemp);
                    }
                    else if (vBinSizeHave > 0)
                    {
                        byte[] vFrimewareBinArrayTemp = new byte[vBinSizeHave + 14];

                        string xx = "$$" + (vBinSizeHave + 14).ToString("D4") + "C09999";
                        byte[] vArrByte = System.Text.Encoding.ASCII.GetBytes(xx);

                        vFrimewareBinArrayTemp[0] = vArrByte[0];
                        vFrimewareBinArrayTemp[1] = vArrByte[1];

                        vFrimewareBinArrayTemp[2] = vArrByte[2];
                        vFrimewareBinArrayTemp[3] = vArrByte[3];
                        vFrimewareBinArrayTemp[4] = vArrByte[4];
                        vFrimewareBinArrayTemp[5] = vArrByte[5];
                        vFrimewareBinArrayTemp[6] = vArrByte[6];
                        vFrimewareBinArrayTemp[7] = vArrByte[7];

                        vFrimewareBinArrayTemp[8] = vArrByte[8];
                        vFrimewareBinArrayTemp[9] = vArrByte[9];
                        vFrimewareBinArrayTemp[10] = vArrByte[10];
                        vFrimewareBinArrayTemp[11] = vArrByte[11];

                        for (int i = 0; i < vBinSizeHave; i++)
                        {
                            vFrimewareBinArrayTemp[i + 12] = vFrimwareBinArray[vBinSizeReadIndex];
                            vBinSizeReadIndex += 1;
                        }
                        byte[] arrByte = System.Text.Encoding.ASCII.GetBytes(CheckSumString(vFrimewareBinArrayTemp, vBinSizeHave + 12));
                        vFrimewareBinArrayTemp[vBinSizeHave + 12] = arrByte[0];//Checksum
                        vFrimewareBinArrayTemp[vBinSizeHave + 13] = arrByte[1];//Checksum
                        FrimwareBinOTAArrayList.Add(vFrimewareBinArrayTemp);
                    }

                } while (vBinSizeHave > 0);
                vBinFileStream.Close();
                this.GPRSpb.Minimum = 0;
                this.GPRSpb.Maximum = 100;
                this.GPRSpb.Step = 1;
                this.OTAbt.Enabled = true;
            }
            catch
            {
            }
        }

        private void UpdateFirmwarebt_Click(object sender, EventArgs e)
        {
            SendData("015", 0xCF);
            this.Updatingpb.Value = 0;
            this.LoadBinbt.Enabled = false;
        }

        private void SaveLogbt_Click(object sender, EventArgs e)
        {
            if (this.LogTb.Text != "")
            {
                this.SaveLogbt.Enabled = false;
                Thread vSaveLog = new Thread(new ThreadStart(SaveLog));
                vSaveLog.Start();
            }
        }
        private void SaveLog()
        {
            string vFileName = AppDomain.CurrentDomain.BaseDirectory + "Log\\";
            if (!Directory.Exists(vFileName))
            {
                Directory.CreateDirectory(vFileName);
            }
            vFileName += DateTime.Now.ToString("yyyyMMddhhmmss") + "Log.txt";
            StreamWriter vStreamWriter = new StreamWriter(vFileName, true);
            vStreamWriter.Write(this.LogTb.Text.ToString());
            vStreamWriter.Close();
            this.LogTb.Text = "";
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe");
            psi.Arguments = " /select," + vFileName;
            System.Diagnostics.Process.Start(psi);
            this.SaveLogbt.Enabled = true;
        }
        //crc校验
        private byte CheckSum(byte[] pSouceByte)
        {

            int vResult = 0;
            for (int i = 0; i < 63; i++)
            {
                vResult ^= pSouceByte[i];
            }
            return (byte)Convert.ToByte(vResult);
        }

        private void tb_send_TextChanged(object sender, EventArgs e)
        {
            if (this.tb_send.Text.Length >= 60)
            {
                this.tb_send.BackColor = Color.LimeGreen;
            }
            else
            {
                this.tb_send.BackColor = System.Drawing.Color.MistyRose;
            }
        }

        private void Sniffer_Load(object sender, EventArgs e)
        {
            tabControl1.DrawItem += new DrawItemEventHandler(tabControl1_DrawItem);
            this.ID.Parent = null;

        }
        private void tabControl1_DrawItem(Object sender, System.Windows.Forms.DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            Brush _textBrush;

            // Get the item from the collection.
            TabPage _tabPage = tabControl1.TabPages[e.Index];

            // Get the real bounds for the tab rectangle.
            Rectangle _tabBounds = tabControl1.GetTabRect(e.Index);

            if (e.State == DrawItemState.Selected)
            {

                // Draw a different background color, and don't paint a focus rectangle.
                _textBrush = new SolidBrush(Color.Black);
                g.FillRectangle(Brushes.Gray, e.Bounds);
            }
            else
            {
                _textBrush = new System.Drawing.SolidBrush(e.ForeColor);
                e.DrawBackground();
            }

            // Use our own font.
            Font _tabFont = new System.Drawing.Font("Arial Narrow", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));

            // Draw string. Center the text.
            StringFormat _stringFlags = new StringFormat();
            _stringFlags.Alignment = StringAlignment.Center;
            _stringFlags.LineAlignment = StringAlignment.Center;
            g.DrawString(_tabPage.Text, _tabFont, _textBrush, _tabBounds, new StringFormat(_stringFlags));
        }

        private void tb_send_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SendData(this.tb_send.Text.Trim(), 0xCF); ;
            }
        }

        private void textBox1_Enter(object sender, EventArgs e)
        {
            if (this.Passwordtb.ForeColor == System.Drawing.SystemColors.GrayText)
            {
                this.Passwordtb.Text = "";
                this.Passwordtb.ForeColor = Color.Black;
                this.Passwordtb.UseSystemPasswordChar = true;
            }
        }

        private void LoadToDevicebt_Click(object sender, EventArgs e)
        {
            SettbForeColorBlack();
            LoadConfigFromTeStringTemp = string.Empty;
            SendData("013", 0xCF);
        }

        private void SaveToDevicebt_Click(object sender, EventArgs e)
        {

            byte[] vConfigbyte = new byte[406];
            byte[] vConfigbytetem = GetConfigBytes();
            if (vConfigbytetem != null)
            {
                Array.Copy(vConfigbytetem, 0, vConfigbyte, 0, 352);
                try
                {
                    int vPageNum = 0;
                    for (int i = 0; i < 7; i++)
                    {
                        vPageNum = i;
                        byte[] ConfigArrayTemp = new byte[64];
                        ConfigArrayTemp[0] = PageHead;
                        ConfigArrayTemp[2] = 0xCD;
                        ConfigArrayTemp[3] = (byte)((0xff00 & vPageNum + 1) >> 8);
                        ConfigArrayTemp[4] = (byte)(0xff & vPageNum + 1);
                        ConfigArrayTemp[1] = 0x3A;//数据长度 
                        Array.Copy(vConfigbyte, i * 58, ConfigArrayTemp, 5, 58);
                        ConfigArrayTemp[63] = CheckSum(ConfigArrayTemp);//Checksum
                        ConfigArrayList.Add(ConfigArrayTemp);
                        //Array.Clear(ConfigArrayTemp,0,64);
                    }
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("Save config to device fail<" + ex.Message + ">");
                    return;
                }
                this.SaveToDevicebt.Enabled = false;
                Thread vSaveLog = new Thread(new ThreadStart(SaveConfigToTE));
                vSaveLog.Start();
            }

        }
        private byte[] GetConfigBytes()
        {
            try
            {
                ConfigArrayList.Clear();
                ASKConfigFlag = 1;
                byte[] vConfigbyte = new byte[352];
                if (this.Passwordtb.Text.Length == 6)
                {
                    Array.Copy(Encoding.ASCII.GetBytes(this.Passwordtb.Text.ToString()), 0, vConfigbyte, 0, 6);
                }
                else
                {
                    MessageBox.Show("Password error");
                    return null;
                }

                if (this.Vehicletb.Text.Length <= 17)
                {
                    Array.Copy(Encoding.ASCII.GetBytes(this.Vehicletb.Text.ToString()), 0, vConfigbyte, 6, Encoding.ASCII.GetBytes(this.Vehicletb.Text.ToString()).Length);
                }
                else
                {
                    MessageBox.Show("Vehicle VIN error");
                    return null;
                }
                if (this.SIMPINtb.Text.Length <= 4)
                {
                    Array.Copy(Encoding.ASCII.GetBytes(this.SIMPINtb.Text.ToString()), 0, vConfigbyte, 23, Encoding.ASCII.GetBytes(this.SIMPINtb.Text.ToString()).Length);
                }
                else
                {
                    MessageBox.Show("SIM PIN error");
                    return null;
                }
                Array.Copy(Encoding.ASCII.GetBytes(this.GsmBandcb.SelectedIndex.ToString()), 0, vConfigbyte, 27, 1);
                if (this.APNtb.Text.Length <= 30)
                {
                    Array.Copy(Encoding.ASCII.GetBytes(this.APNtb.Text.ToString()), 0, vConfigbyte, 28, Encoding.ASCII.GetBytes(this.APNtb.Text.ToString()).Length);
                }
                else
                {
                    MessageBox.Show("APN error");
                    return null;
                }
                if (this.APNUserNametb.Text.Length <= 20)
                {
                    Array.Copy(Encoding.ASCII.GetBytes(this.APNUserNametb.Text.ToString()), 0, vConfigbyte, 58, Encoding.ASCII.GetBytes(this.APNUserNametb.Text.ToString()).Length);
                }
                else
                {
                    MessageBox.Show("APN user name error");
                    return null;
                }
                if (this.APNPasswordtb.Text.Length <= 20)
                {
                    Array.Copy(Encoding.ASCII.GetBytes(this.APNPasswordtb.Text.ToString()), 0, vConfigbyte, 78, Encoding.ASCII.GetBytes(this.APNPasswordtb.Text.ToString()).Length);
                }
                else
                {
                    MessageBox.Show("APN password error");
                    return null;
                }
                Array.Copy(Encoding.ASCII.GetBytes(this.DomailIpcb.SelectedIndex.ToString()), 0, vConfigbyte, 98, 1);
                if (this.ServerDomainIptb.Text.Length <= 30)
                {
                    Array.Copy(Encoding.ASCII.GetBytes(this.ServerDomainIptb.Text.ToString()), 0, vConfigbyte, 99, Encoding.ASCII.GetBytes(this.ServerDomainIptb.Text.ToString()).Length);
                }
                else
                {
                    MessageBox.Show("Server IP error");
                    return null;
                }
                if (this.ServerPorttb.Text.Length <= 5)
                {
                    Array.Copy(Encoding.ASCII.GetBytes(this.ServerPorttb.Text.ToString()), 0, vConfigbyte, 129, Encoding.ASCII.GetBytes(this.ServerPorttb.Text.ToString()).Length);
                }
                else
                {
                    MessageBox.Show("Server port error");
                    return null;
                }
                Array.Copy(Encoding.ASCII.GetBytes(this.ServerProtocolcb.SelectedIndex.ToString()), 0, vConfigbyte, 134, 1);

                Array.Copy(Encoding.ASCII.GetBytes(this.ServerResporiseTimeoutud.Value.ToString()), 0, vConfigbyte, 135, Encoding.ASCII.GetBytes(this.ServerResporiseTimeoutud.Value.ToString()).Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.Heartbeatud.Value.ToString()), 0, vConfigbyte, 139, Encoding.ASCII.GetBytes(this.Heartbeatud.Value.ToString()).Length);

                Array.Copy(Encoding.ASCII.GetBytes(this.MovementDetectionIgnitioncb.SelectedIndex.ToString()), 0, vConfigbyte, 143, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.MovementDetection3Dcb.SelectedIndex.ToString()), 0, vConfigbyte, 144, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.MovementDetectionDelayud.Value.ToString()), 0, vConfigbyte, 145, Encoding.ASCII.GetBytes(this.MovementDetectionDelayud.Value.ToString()).Length);

                //Scenario1
                Array.Copy(Encoding.ASCII.GetBytes(this.MovingTimeud1.Value.ToString()), 0, vConfigbyte, 149, this.MovingTimeud1.Value.ToString().Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.MovingDistanceud1.Value.ToString()), 0, vConfigbyte, 154, this.MovingDistanceud1.Value.ToString().Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.MovingAngleud1.Value.ToString()), 0, vConfigbyte, 159, this.MovingAngleud1.Value.ToString().Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.StopTimeud1.Value.ToString()), 0, vConfigbyte, 162, this.StopTimeud1.Value.ToString().Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.Sleepcb1.SelectedIndex.ToString()), 0, vConfigbyte, 167, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.SleepDelayud1.Value.ToString()), 0, vConfigbyte, 168, this.SleepDelayud1.Value.ToString().Length);// 

                //OverSpeed
                Array.Copy(Encoding.ASCII.GetBytes(this.OverSpeedcb.SelectedIndex.ToString()), 0, vConfigbyte, 172, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.OverSpeedud.Value.ToString()), 0, vConfigbyte, 173, this.OverSpeedud.Value.ToString().Length);
                //Monitor
                Array.Copy(Encoding.ASCII.GetBytes(this.Autoansercb.SelectedIndex.ToString()), 0, vConfigbyte, 176, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.Authorizedphone1tb.Text.ToString()), 0, vConfigbyte, 177, Encoding.ASCII.GetBytes(this.Authorizedphone1tb.Text.ToString()).Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.Authorizedphone2tb.Text.ToString()), 0, vConfigbyte, 197, Encoding.ASCII.GetBytes(this.Authorizedphone2tb.Text.ToString()).Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.Authorizedphone3tb.Text.ToString()), 0, vConfigbyte, 217, Encoding.ASCII.GetBytes(this.Authorizedphone3tb.Text.ToString()).Length);
                //Digital IN/OUT
                Array.Copy(Encoding.ASCII.GetBytes(this.AD2IN1cb.SelectedIndex.ToString()), 0, vConfigbyte, 237, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN2cb.SelectedIndex.ToString()), 0, vConfigbyte, 238, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN3cb.SelectedIndex.ToString()), 0, vConfigbyte, 239, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN4cb.SelectedIndex.ToString()), 0, vConfigbyte, 240, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN5cb.SelectedIndex.ToString()), 0, vConfigbyte, 241, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.Output1cb.SelectedIndex.ToString()), 0, vConfigbyte, 242, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.Output2cb.SelectedIndex.ToString()), 0, vConfigbyte, 243, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.Output3cb.SelectedIndex.ToString()), 0, vConfigbyte, 244, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.Output4cb.SelectedIndex.ToString()), 0, vConfigbyte, 245, 1);
                //Analog Input1
                Array.Copy(Encoding.ASCII.GetBytes(this.AD1cb.SelectedIndex.ToString()), 0, vConfigbyte, 246, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.AD1Lowlevelnd.Value.ToString()), 0, vConfigbyte, 247, this.AD1Lowlevelnd.Value.ToString().Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.AD1Highlevelnd.Value.ToString()), 0, vConfigbyte, 252, this.AD1Highlevelnd.Value.ToString().Length);
                //Analog Input2
                Array.Copy(Encoding.ASCII.GetBytes(this.AD2cb.SelectedIndex.ToString()), 0, vConfigbyte, 257, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.AD2Lowlevelnd.Value.ToString()), 0, vConfigbyte, 258, this.AD2Lowlevelnd.Value.ToString().Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.AD2Highlevelnd.Value.ToString()), 0, vConfigbyte, 263, this.AD2Highlevelnd.Value.ToString().Length);
                //Analog Input3
                Array.Copy(Encoding.ASCII.GetBytes(this.AD3cb.SelectedIndex.ToString()), 0, vConfigbyte, 268, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.AD3Lowlevelnd.Value.ToString()), 0, vConfigbyte, 269, this.AD3Lowlevelnd.Value.ToString().Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.AD3Highlevelnd.Value.ToString()), 0, vConfigbyte, 274, this.AD3Highlevelnd.Value.ToString().Length);
                //Analog Input4
                Array.Copy(Encoding.ASCII.GetBytes(this.AD4cb.SelectedIndex.ToString()), 0, vConfigbyte, 279, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.AD4Lowlevelnd.Value.ToString()), 0, vConfigbyte, 280, this.AD4Lowlevelnd.Value.ToString().Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.AD4Highlevelnd.Value.ToString()), 0, vConfigbyte, 285, this.AD4Highlevelnd.Value.ToString().Length);


                //GEO
                Array.Copy(Encoding.ASCII.GetBytes(this.GeoEntereventcb.SelectedIndex.ToString()), 0, vConfigbyte, 290, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.GeoExiteventcb.SelectedIndex.ToString()), 0, vConfigbyte, 291, 1);

                Array.Copy(Encoding.ASCII.GetBytes(this.GeoRadiusud.Value.ToString()), 0, vConfigbyte, 292, this.GeoRadiusud.Value.ToString().Length);
                Array.Copy(Encoding.ASCII.GetBytes(this.GeoLat1tb.Text.ToString()), 0, vConfigbyte, 296, Encoding.ASCII.GetBytes(this.GeoLat1tb.Text.ToString()).Length);//10
                Array.Copy(Encoding.ASCII.GetBytes(this.GeoLong1tb.Text.ToString()), 0, vConfigbyte, 306, Encoding.ASCII.GetBytes(this.GeoLong1tb.Text.ToString()).Length);//11             

                //SMS Alarm
                Array.Copy(Encoding.ASCII.GetBytes(this.Gpsantennacutcb.SelectedIndex.ToString()), 0, vConfigbyte, 317, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.Externalpowercutcb.SelectedIndex.ToString()), 0, vConfigbyte, 318, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.Towcb.SelectedIndex.ToString()), 0, vConfigbyte, 319, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.SOScb.SelectedIndex.ToString()), 0, vConfigbyte, 320, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.GEOINcb.SelectedIndex.ToString()), 0, vConfigbyte, 321, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.GEOOUTcb.SelectedIndex.ToString()), 0, vConfigbyte, 322, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN1ONcb.SelectedIndex.ToString()), 0, vConfigbyte, 323, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN1OFFcb.SelectedIndex.ToString()), 0, vConfigbyte, 324, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN2ONcb.SelectedIndex.ToString()), 0, vConfigbyte, 325, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN2OFFcb.SelectedIndex.ToString()), 0, vConfigbyte, 326, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN3ONcb.SelectedIndex.ToString()), 0, vConfigbyte, 327, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN3OFFcb.SelectedIndex.ToString()), 0, vConfigbyte, 328, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN4ONcb.SelectedIndex.ToString()), 0, vConfigbyte, 329, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN5ONcb.SelectedIndex.ToString()), 0, vConfigbyte, 330, 1);
                Array.Copy(Encoding.ASCII.GetBytes(this.IN5OFFcb.SelectedIndex.ToString()), 0, vConfigbyte, 331, 1);
                //   
                if (this.ServerPorttb.Text.Length <= 20)
                {
                    Array.Copy(Encoding.ASCII.GetBytes(this.SMSPhonetb.Text.ToString()), 0, vConfigbyte, 332, Encoding.ASCII.GetBytes(this.SMSPhonetb.Text.ToString()).Length);
                }
                else
                {
                    MessageBox.Show("Server port error");
                    return null;
                }
                try
                {
                    //FileStream fs = new FileStream("Confgi.bin", FileMode.Create);
                    //fs.Write(vConfigbyte, 0, vConfigbyte.Length);
                    //fs.Flush();
                    //fs.Close();
                    // this.WriteConfigToFile(SaveConfig.FileName);
                    // Tools.ShowMessage("Config data is written to the file \n" + SaveConfig.FileName + " !", MessageBoxIcon.Information);

                    //  ClearContols();
                }
                catch (Exception ex)
                {
                    // Tools.ShowMessage(string.Format("Error:{0}", ex.Message), MessageBoxIcon.Information);
                }
                return vConfigbyte;
            }
            catch (Exception ex)
            {
                // Tools.ShowMessage(string.Format("Error:{0}", ex.Message), MessageBoxIcon.Information);                 
            }
            return null;
        }
        private void SaveConfigToTE()
        {
            bool vSendFlag = true;
            while (vSendFlag)
            {
                switch (ASKConfigFlag)
                {
                    case 1:
                        vSendFlag = USBDataSend(ConfigArrayList[0]);
                        break;
                    case 2:
                        vSendFlag = USBDataSend(ConfigArrayList[1]);
                        break;
                    case 3:
                        vSendFlag = USBDataSend(ConfigArrayList[2]);
                        break;
                    case 4:
                        vSendFlag = USBDataSend(ConfigArrayList[3]);
                        break;
                    case 5:
                        vSendFlag = USBDataSend(ConfigArrayList[4]);
                        break;
                    case 6:
                        vSendFlag = USBDataSend(ConfigArrayList[5]);
                        break;
                    case 7:
                        vSendFlag = USBDataSend(ConfigArrayList[6]);
                        this.SaveToDevicebt.Enabled = true;
                        DisplayLog("Save success\r\n");
                        break;
                    default:
                        vSendFlag = false;
                        break;
                }
                Thread.Sleep(100);
            }

        }
        private void InitializeDevicebt_Click(object sender, EventArgs e)
        {
            SendData("007", 0xCF);
        }

        private void LoadToFilebt_Click(object sender, EventArgs e)
        {
            //导入配置文件
            SettbForeColorBlack();
            LoadConfigFromFile();

        }

        private void SaveToFilebt_Click(object sender, EventArgs e)
        {
            SaveConfigtoFile();
        }

        private void iButtonIDgv_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            try
            {

                e.Graphics.DrawString((e.RowIndex + 1).ToString(), e.InheritedRowStyle.Font, new SolidBrush(Color.CadetBlue), e.RowBounds.Location.X + 15, e.RowBounds.Location.Y + 5);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private void LogTb_TextChanged(object sender, EventArgs e)
        {
            this.LogTb.SelectionStart = this.LogTb.Text.Length;
            this.LogTb.SelectionLength = 0;
            this.LogTb.ScrollToCaret();
        }

        private void SaveConfigtoFile()
        {
            SaveToFiledg.Filter = "XML files(*.xml)|*.xml";
            SaveToFiledg.FileName = "Config";
            if (SaveToFiledg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string filepath = SaveToFiledg.FileName;
                    XmlDocument vXmldoc = new XmlDocument();
                    XmlDeclaration vXmldecl;
                    XmlElement vXmlelem;
                    vXmldecl = vXmldoc.CreateXmlDeclaration("1.0", "UTF-8", null);
                    vXmldoc.AppendChild(vXmldecl);
                    vXmlelem = vXmldoc.CreateElement("", "Config", "");
                    vXmldoc.AppendChild(vXmlelem);

                    XmlNode vRoot = vXmldoc.SelectSingleNode("Config");

                    XmlElement vVehicleVid = vXmldoc.CreateElement("VehicleVid");
                    vVehicleVid.InnerText = this.Vehicletb.Text.ToString();
                    vRoot.AppendChild(vVehicleVid);


                    XmlElement vSIMPIN = vXmldoc.CreateElement("SIMPIN");
                    vSIMPIN.InnerText = this.SIMPINtb.Text.ToString();
                    vRoot.AppendChild(vSIMPIN);

                    XmlElement vGSMBand = vXmldoc.CreateElement("GSMBand");
                    vGSMBand.InnerText = this.GsmBandcb.SelectedIndex.ToString();
                    vRoot.AppendChild(vGSMBand);

                    XmlElement vAPN = vXmldoc.CreateElement("APN");
                    vAPN.InnerText = this.APNtb.Text.ToString();
                    vRoot.AppendChild(vAPN);

                    XmlElement vAPNUserName = vXmldoc.CreateElement("APNUserName");
                    vAPNUserName.InnerText = this.APNUserNametb.Text.ToString();
                    vRoot.AppendChild(vAPNUserName);

                    XmlElement vAPNPassword = vXmldoc.CreateElement("APNPassword");
                    vAPNPassword.InnerText = this.APNPasswordtb.Text.ToString();
                    vRoot.AppendChild(vAPNPassword);

                    XmlElement vDomainorIp = vXmldoc.CreateElement("DomainorIp ");
                    vDomainorIp.InnerText = this.DomailIpcb.SelectedIndex.ToString();
                    vRoot.AppendChild(vDomainorIp);

                    XmlElement vDomain = vXmldoc.CreateElement("Domain");
                    vDomain.InnerText = this.ServerDomainIptb.Text.ToString();
                    vRoot.AppendChild(vDomain);

                    XmlElement vPort = vXmldoc.CreateElement("Port");
                    vPort.InnerText = this.ServerPorttb.Text.ToString();
                    vRoot.AppendChild(vPort);

                    XmlElement vProtocol = vXmldoc.CreateElement("Protocol");
                    vProtocol.InnerText = this.ServerProtocolcb.SelectedIndex.ToString();
                    vRoot.AppendChild(vProtocol);

                    XmlElement vServerResponseTimeout = vXmldoc.CreateElement("ServerResponseTimeout");
                    vServerResponseTimeout.InnerText = this.ServerResporiseTimeoutud.Value.ToString();
                    vRoot.AppendChild(vServerResponseTimeout);

                    XmlElement vHeartbeat = vXmldoc.CreateElement("Heartbeat");
                    vHeartbeat.InnerText = this.Heartbeatud.Value.ToString();
                    vRoot.AppendChild(vHeartbeat);

                    XmlElement vMovementDetectionIgnition = vXmldoc.CreateElement("MovementDetectionIgnition");
                    vMovementDetectionIgnition.InnerText = this.MovementDetectionIgnitioncb.SelectedIndex.ToString();
                    vRoot.AppendChild(vMovementDetectionIgnition);

                    XmlElement vMovement3D = vXmldoc.CreateElement("Movement3D");
                    vMovement3D.InnerText = this.MovementDetection3Dcb.SelectedIndex.ToString();
                    vRoot.AppendChild(vMovement3D);

                    XmlElement vMovementDelay = vXmldoc.CreateElement("MovementDelay");
                    vMovementDelay.InnerText = this.MovementDetectionDelayud.Value.ToString();
                    vRoot.AppendChild(vMovementDelay);

                    XmlElement vMovingTime1 = vXmldoc.CreateElement("MovingTime1");
                    vMovingTime1.InnerText = this.MovingTimeud1.Value.ToString();
                    vRoot.AppendChild(vMovingTime1);

                    XmlElement vMovingDistance1 = vXmldoc.CreateElement("MovingDistance1");
                    vMovingDistance1.InnerText = this.MovingDistanceud1.Value.ToString();
                    vRoot.AppendChild(vMovingDistance1);

                    XmlElement vMovingAngle1 = vXmldoc.CreateElement("MovingAngle1");
                    vMovingAngle1.InnerText = this.MovingAngleud1.Value.ToString();
                    vRoot.AppendChild(vMovingAngle1);

                    XmlElement vStopTime1 = vXmldoc.CreateElement("StopTime1");
                    vStopTime1.InnerText = this.StopTimeud1.Value.ToString();
                    vRoot.AppendChild(vStopTime1);

                    XmlElement vSleep1 = vXmldoc.CreateElement("Sleep1");
                    vSleep1.InnerText = this.Sleepcb1.SelectedIndex.ToString();
                    vRoot.AppendChild(vSleep1);

                    XmlElement vSleepDelay1 = vXmldoc.CreateElement("SleepDelay1");
                    vSleepDelay1.InnerText = this.SleepDelayud1.Value.ToString();
                    vRoot.AppendChild(vSleepDelay1);

                    XmlElement vOverSpeed = vXmldoc.CreateElement("OverSpeed");
                    vOverSpeed.InnerText = this.OverSpeedcb.SelectedIndex.ToString();
                    vRoot.AppendChild(vOverSpeed);


                    XmlElement vOverSpeedValue = vXmldoc.CreateElement("OverSpeedValue");
                    vOverSpeedValue.InnerText = this.OverSpeedud.Value.ToString();
                    vRoot.AppendChild(vOverSpeedValue);

                    XmlElement vMonitorAutoanswer = vXmldoc.CreateElement("MonitorAutoanswer");
                    vMonitorAutoanswer.InnerText = this.Autoansercb.SelectedIndex.ToString();
                    vRoot.AppendChild(vMonitorAutoanswer);

                    XmlElement vAuthorizedPhone1 = vXmldoc.CreateElement("AuthorizedPhone1");
                    vAuthorizedPhone1.InnerText = this.Authorizedphone1tb.Text.ToString();
                    vRoot.AppendChild(vAuthorizedPhone1);

                    XmlElement vAuthorizedPhone2 = vXmldoc.CreateElement("AuthorizedPhone2");
                    vAuthorizedPhone2.InnerText = this.Authorizedphone2tb.Text.ToString();
                    vRoot.AppendChild(vAuthorizedPhone2);

                    XmlElement vAuthorizedPhone3 = vXmldoc.CreateElement("AuthorizedPhone3");
                    vAuthorizedPhone3.InnerText = this.Authorizedphone3tb.Text.ToString();
                    vRoot.AppendChild(vAuthorizedPhone3);

                    XmlElement vAD2orIN1 = vXmldoc.CreateElement("AD2orIN1");
                    vAD2orIN1.InnerText = this.AD2IN1cb.SelectedIndex.ToString();
                    vRoot.AppendChild(vAD2orIN1);

                    XmlElement vIN2 = vXmldoc.CreateElement("IN2");
                    vIN2.InnerText = this.IN2cb.SelectedIndex.ToString();
                    vRoot.AppendChild(vIN2);

                    XmlElement vIN3 = vXmldoc.CreateElement("IN3");
                    vIN3.InnerText = this.IN3cb.SelectedIndex.ToString();
                    vRoot.AppendChild(vIN3);

                    XmlElement vIN4 = vXmldoc.CreateElement("IN4");
                    vIN4.InnerText = this.IN4cb.SelectedIndex.ToString();
                    vRoot.AppendChild(vIN4);

                    XmlElement vOutPut1 = vXmldoc.CreateElement("OutPut1");
                    vOutPut1.InnerText = this.Output1cb.SelectedIndex.ToString();
                    vRoot.AppendChild(vOutPut1);

                    XmlElement vOutPut2 = vXmldoc.CreateElement("OutPut2");
                    vOutPut2.InnerText = this.Output2cb.SelectedIndex.ToString();
                    vRoot.AppendChild(vOutPut2);

                    XmlElement vOutPut3 = vXmldoc.CreateElement("OutPut3");
                    vOutPut3.InnerText = this.Output3cb.SelectedIndex.ToString();
                    vRoot.AppendChild(vOutPut3);

                    XmlElement vAD1 = vXmldoc.CreateElement("AD1");
                    vAD1.InnerText = this.AD1cb.SelectedIndex.ToString();
                    vRoot.AppendChild(vAD1);

                    XmlElement vAD1Lowlevel = vXmldoc.CreateElement("AD1Lowlevel");
                    vAD1Lowlevel.InnerText = this.AD1Lowlevelnd.Value.ToString();
                    vRoot.AppendChild(vAD1Lowlevel);

                    XmlElement vAD1Highlevel = vXmldoc.CreateElement("AD1Highlevel");
                    vAD1Highlevel.InnerText = this.AD1Highlevelnd.Value.ToString();
                    vRoot.AppendChild(vAD1Highlevel);

                    XmlElement vGeoEnterevent = vXmldoc.CreateElement("GeoEnterevent");
                    vGeoEnterevent.InnerText = this.GeoEntereventcb.SelectedIndex.ToString();
                    vRoot.AppendChild(vGeoEnterevent);

                    XmlElement vGeoExitevent = vXmldoc.CreateElement("GeoExitevent");
                    vGeoExitevent.InnerText = this.GeoExiteventcb.SelectedIndex.ToString();
                    vRoot.AppendChild(vGeoExitevent);

                    XmlElement vGeoRadius = vXmldoc.CreateElement("GeoRadius");
                    vGeoRadius.InnerText = this.GeoRadiusud.Value.ToString();
                    vRoot.AppendChild(vGeoRadius);

                    XmlElement vGeoLat1 = vXmldoc.CreateElement("GeoLat1");
                    vGeoLat1.InnerText = this.GeoLat1tb.Text.ToString();
                    vRoot.AppendChild(vGeoLat1);

                    XmlElement vGeoLong1 = vXmldoc.CreateElement("GeoLong1");
                    vGeoLong1.InnerText = this.GeoLong1tb.Text.ToString();
                    vRoot.AppendChild(vGeoLong1);

                    XmlElement vTargetPhone = vXmldoc.CreateElement("TargetPhone");
                    vTargetPhone.InnerText = this.SMSPhonetb.Text.ToString();
                    vRoot.AppendChild(vTargetPhone);

                    //XmlElement vSMSAlarmSOS = vXmldoc.CreateElement("SMSAlarmSOS");
                    //vSMSAlarmSOS.InnerText = this.SMSAlarmSOScb.SelectedIndex.ToString();
                    //vRoot.AppendChild(vSMSAlarmSOS);

                    //XmlElement vSMSAlarmSOSContent = vXmldoc.CreateElement("SMSAlarmSOSContent");
                    //vSMSAlarmSOSContent.InnerText = this.SMSAlarmSOStb.Text.ToString();
                    //vRoot.AppendChild(vSMSAlarmSOSContent);

                    //XmlElement vSMSAlarmGEO = vXmldoc.CreateElement("SMSAlarmGEO");
                    //vSMSAlarmGEO.InnerText = this.SMSAlarmGeocb.SelectedIndex.ToString();
                    //vRoot.AppendChild(vSMSAlarmGEO);

                    //XmlElement vSMSAlarmGEOContent = vXmldoc.CreateElement("SMSAlarmGEOContent");
                    //vSMSAlarmGEOContent.InnerText = this.SMSAlarmGeotb.Text.ToString();
                    //vRoot.AppendChild(vSMSAlarmGEOContent);

                    //XmlElement vSMSAlarmTow = vXmldoc.CreateElement("SMSAlarmTow");
                    //vSMSAlarmTow.InnerText = this.SMSAlarmTowcb.SelectedIndex.ToString();
                    //vRoot.AppendChild(vSMSAlarmTow);

                    //XmlElement vSMSAlarmTowContent = vXmldoc.CreateElement("SMSAlarmTowContent");
                    //vSMSAlarmTowContent.InnerText = this.SMSAlarmTowtb.Text.ToString();
                    //vRoot.AppendChild(vSMSAlarmTowContent);

                    //XmlElement vSMSAlarmGpsantennacut = vXmldoc.CreateElement("SMSAlarmGpsantennacut");
                    //vSMSAlarmGpsantennacut.InnerText = this.SMSAlarmGpsantennacutcb.SelectedIndex.ToString();
                    //vRoot.AppendChild(vSMSAlarmGpsantennacut);

                    //XmlElement vSMSAlarmGpsantennacutContent = vXmldoc.CreateElement("SMSAlarmGpsantennacutContent");
                    //vSMSAlarmGpsantennacutContent.InnerText = this.SMSAlarmGpsantennacuttb.Text.ToString();
                    //vRoot.AppendChild(vSMSAlarmGpsantennacutContent);

                    //XmlElement vSMSAlarmExternalpowercut = vXmldoc.CreateElement("SMSAlarmExternalpowercut");
                    //vSMSAlarmExternalpowercut.InnerText = this.SMSAlarmExternalpowercutcb.SelectedIndex.ToString();
                    //vRoot.AppendChild(vSMSAlarmExternalpowercut);

                    //XmlElement vSMSAlarmExteranlpowercutContent = vXmldoc.CreateElement("SMSAlarmExteranlpowercutContent");
                    //vSMSAlarmExteranlpowercutContent.InnerText = this.SMSAlarmExternalpowercuttb.Text.ToString();
                    //vRoot.AppendChild(vSMSAlarmExteranlpowercutContent);

                    //XmlElement vSMSAlarmLowbattery = vXmldoc.CreateElement("SMSAlarmLowbattery");
                    //vSMSAlarmLowbattery.InnerText = this.SMSAlarmLowbatterycb.SelectedIndex.ToString();
                    //vRoot.AppendChild(vSMSAlarmLowbattery);

                    //XmlElement vSMSAlarmLowbatteryContent = vXmldoc.CreateElement("SMSAlarmLowbatteryContent");
                    //vSMSAlarmLowbatteryContent.InnerText = this.SMSAlarmLowbatterytb.Text.ToString();
                    //vRoot.AppendChild(vSMSAlarmLowbatteryContent);

                    //XmlElement vSMSAlarmIN1 = vXmldoc.CreateElement("SMSAlarmIN1");
                    //vSMSAlarmIN1.InnerText = this.SMSAlarmIN1cb.SelectedIndex.ToString();
                    //vRoot.AppendChild(vSMSAlarmIN1);

                    //XmlElement vSMSAlarmIN1Content = vXmldoc.CreateElement("SMSAlarmIN1Content");
                    //vSMSAlarmIN1Content.InnerText = this.SMSAlarmIN1tb.Text.ToString();
                    //vRoot.AppendChild(vSMSAlarmIN1Content);

                    //XmlElement vSMSAlarmIN2 = vXmldoc.CreateElement("SMSAlarmIN2");
                    //vSMSAlarmIN1.InnerText = this.SMSAlarmIN2cb.SelectedIndex.ToString();
                    //vRoot.AppendChild(vSMSAlarmIN2);

                    //XmlElement vSMSAlarmIN2Content = vXmldoc.CreateElement("SMSAlarmIN2Content");
                    //vSMSAlarmIN2Content.InnerText = this.SMSAlarmIN2tb.Text.ToString();
                    //vRoot.AppendChild(vSMSAlarmIN2Content);

                    //XmlElement vSMSAlarmIN3 = vXmldoc.CreateElement("SMSAlarmIN3");
                    //vSMSAlarmIN3.InnerText = this.SMSAlarmIN2cb.SelectedIndex.ToString();
                    //vRoot.AppendChild(vSMSAlarmIN3);

                    //XmlElement vSMSAlarmIN3Content = vXmldoc.CreateElement("SMSAlarmIN3Content");
                    //vSMSAlarmIN3Content.InnerText = this.SMSAlarmIN3tb.Text.ToString();
                    //vRoot.AppendChild(vSMSAlarmIN3Content);

                    //XmlElement vSMSAlarmIN4 = vXmldoc.CreateElement("SMSAlarmIN4");
                    //vSMSAlarmIN4.InnerText = this.SMSAlarmIN2cb.SelectedIndex.ToString();
                    //vRoot.AppendChild(vSMSAlarmIN4);

                    //XmlElement vSMSAlarmIN4Content = vXmldoc.CreateElement("SMSAlarmIN4Content");
                    //vSMSAlarmIN4Content.InnerText = this.SMSAlarmIN4tb.Text.ToString();
                    //vRoot.AppendChild(vSMSAlarmIN4Content);

                    vXmldoc.Save(filepath);
                    MessageBox.Show("Save file successful");
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("Save file error <" + ex.Message + ">");
                }

            }



        }
        private void LoadConfigFromFile()
        {
            LoadedConfigFromFiledg.Filter = "XML files(*.xml)|*.xml";
            LoadedConfigFromFiledg.FileName = "Config";
            if (LoadedConfigFromFiledg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string filepath = LoadedConfigFromFiledg.FileName;
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(filepath);
                    XmlNode vXmlNode = xmlDoc.SelectSingleNode("Config");
                    this.Vehicletb.Text = vXmlNode.SelectSingleNode("VehicleVid").InnerText;
                    this.SIMPINtb.Text = vXmlNode.SelectSingleNode("SIMPIN").InnerText;
                    this.GsmBandcb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("GSMBand").InnerText);
                    this.APNtb.Text = vXmlNode.SelectSingleNode("APN").InnerText;
                    this.APNUserNametb.Text = vXmlNode.SelectSingleNode("APNUserName").InnerText;
                    this.APNPasswordtb.Text = vXmlNode.SelectSingleNode("APNPassword").InnerText;
                    this.DomailIpcb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("DomainorIp").InnerText);
                    this.ServerDomainIptb.Text = vXmlNode.SelectSingleNode("Domain").InnerText;
                    this.ServerPorttb.Text = vXmlNode.SelectSingleNode("Port").InnerText;
                    this.ServerProtocolcb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("Protocol").InnerText);
                    this.ServerResporiseTimeoutud.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("ServerResponseTimeout").InnerText);
                    this.Heartbeatud.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("Heartbeat").InnerText);
                    this.MovementDetectionIgnitioncb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("MovementDetectionIgnition").InnerText);
                    this.MovementDetection3Dcb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("Movement3D").InnerText);
                    this.MovementDetectionDelayud.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("MovementDelay").InnerText);// 
                    //Scenario1
                    this.MovingTimeud1.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("MovingTime1").InnerText);
                    this.MovingDistanceud1.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("MovingDistance1").InnerText);
                    this.MovingAngleud1.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("MovingAngle1").InnerText);
                    this.StopTimeud1.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("StopTime1").InnerText);
                    this.Sleepcb1.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("Sleep1").InnerText);
                    this.SleepDelayud1.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("SleepDelay1").InnerText);



                    //Alarms
                    this.OverSpeedcb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("OverSpeed").InnerText);
                    this.OverSpeedud.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("OverSpeedValue").InnerText);
                    //Monitor
                    this.Autoansercb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("MonitorAutoanswer").InnerText);
                    this.Authorizedphone1tb.Text = vXmlNode.SelectSingleNode("AuthorizedPhone1").InnerText;
                    this.Authorizedphone2tb.Text = vXmlNode.SelectSingleNode("AuthorizedPhone2").InnerText;
                    this.Authorizedphone3tb.Text = vXmlNode.SelectSingleNode("AuthorizedPhone3").InnerText;
                    //Digital IN/Out
                    this.AD2IN1cb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("AD2orIN1").InnerText);
                    this.IN2cb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("IN2").InnerText);
                    this.IN3cb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("IN3").InnerText);
                    this.IN4cb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("IN4").InnerText);
                    this.Output1cb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("OutPut1").InnerText);
                    this.Output2cb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("OutPut2").InnerText);
                    this.Output3cb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("OutPut3").InnerText);
                    //Analog Input

                    this.AD1cb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("AD1").InnerText);
                    this.AD1Lowlevelnd.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("AD1Lowlevel").InnerText);
                    this.AD1Highlevelnd.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("AD1Highlevel").InnerText);

                    //Geo                 
                    this.GeoEntereventcb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("GeoEnterevent").InnerText);
                    this.GeoExiteventcb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("GeoExitevent").InnerText);
                    this.GeoRadiusud.Value = Convert.ToInt32(vXmlNode.SelectSingleNode("GeoRadius").InnerText);
                    this.GeoLat1tb.Text = vXmlNode.SelectSingleNode("GeoLat1").InnerText;
                    this.GeoLong1tb.Text = vXmlNode.SelectSingleNode("GeoLong1").InnerText;

                    //SMSAlarm
                    this.SMSPhonetb.Text = vXmlNode.SelectSingleNode("TargetPhone").InnerText;
                    //this.SMSAlarmSOScb.SelectedIndex = Convert.ToInt32(vXmlNode.SelectSingleNode("SMSAlarmSOS").InnerText);


                    MessageBox.Show("Successfully loaded");
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("Invalid format of configuration files<" + ex.Message + ">");
                }

            }
        }

        private void RebootDevicebt_Click(object sender, EventArgs e)
        {
            //重启设备
            SendData("006", 0xCF);
        }

        private void Vehicletb_Enter(object sender, EventArgs e)
        {
            if (this.Vehicletb.ForeColor == System.Drawing.SystemColors.GrayText)
            {
                this.Vehicletb.Text = "";
                this.Vehicletb.ForeColor = Color.Black;
            }
        }

        private void SIMPINtb_Enter(object sender, EventArgs e)
        {
            if (this.SIMPINtb.ForeColor == System.Drawing.SystemColors.GrayText)
            {
                this.SIMPINtb.Text = "";
                this.SIMPINtb.ForeColor = Color.Black;
            }
        }

        private void APNtb_Enter(object sender, EventArgs e)
        {
            if (this.APNtb.ForeColor == System.Drawing.SystemColors.GrayText)
            {
                this.APNtb.Text = "";
                this.APNtb.ForeColor = Color.Black;
            }
        }

        private void APNUserNametb_Enter(object sender, EventArgs e)
        {
            if (this.APNUserNametb.ForeColor == System.Drawing.SystemColors.GrayText)
            {
                this.APNUserNametb.Text = "";
                this.APNUserNametb.ForeColor = Color.Black;
            }
        }

        private void APNPasswordtb_Enter(object sender, EventArgs e)
        {
            if (this.APNPasswordtb.ForeColor == System.Drawing.SystemColors.GrayText)
            {
                this.APNPasswordtb.Text = "";
                this.APNPasswordtb.ForeColor = Color.Black;
            }
        }

        private void ServerDomainIptb_Enter(object sender, EventArgs e)
        {
            if (this.ServerDomainIptb.ForeColor == System.Drawing.SystemColors.GrayText)
            {
                this.ServerDomainIptb.Text = "";
                this.ServerDomainIptb.ForeColor = Color.Black;
            }
        }

        private void ServerPorttb_Enter(object sender, EventArgs e)
        {
            if (this.ServerPorttb.ForeColor == System.Drawing.SystemColors.GrayText)
            {
                this.ServerPorttb.Text = "";
                this.ServerPorttb.ForeColor = Color.Black;
            }
        }
        private void SettbForeColorBlack()
        {
            this.ServerPorttb.ForeColor = Color.Black;
            this.ServerDomainIptb.ForeColor = Color.Black;
            this.APNPasswordtb.ForeColor = Color.Black;
            this.APNUserNametb.ForeColor = Color.Black;
            this.APNtb.ForeColor = Color.Black;
            this.Vehicletb.ForeColor = Color.Black;
        }

        private void ServerProtocolcb_DrawItem(object sender, DrawItemEventArgs e)
        {
            string s = this.ServerProtocolcb.Items[e.Index].ToString();
            SizeF ss = e.Graphics.MeasureString(s, e.Font);

            float l = (float)(e.Bounds.Width - ss.Width) / 2;
            if (l < 0) l = 0f;
            float t = (float)(e.Bounds.Height - ss.Height) / 2;
            if (t < 0) t = 0f;
            t = t + this.ServerProtocolcb.ItemHeight * e.Index;
            e.DrawBackground();
            e.DrawFocusRectangle();
            e.Graphics.DrawString(s, e.Font, new SolidBrush(e.ForeColor), l, t);
        }


        private void TCPListenbt_Click(object sender, EventArgs e)
        {
            if (this.UnitTestPorttb.Enabled)
            {
                try
                {
                    MainTest();
                    this.UnitTestPorttb.Enabled = false;
                }
                catch { }

            }
            else
            {
                try
                {
                    server.Stop(); this.UnitTestPorttb.Enabled = true;
                }
                catch
                {
                }

            }
        }
        private void MainTest()
        {
            server = new AsyncTcpServer(Convert.ToInt32(this.UnitTestPorttb.Text.Trim()));
            server.Encoding = Encoding.ASCII;
            server.ClientConnected +=
              new EventHandler<TcpClientConnectedEventArgs>(server_ClientConnected);
            server.ClientDisconnected +=
              new EventHandler<TcpClientDisconnectedEventArgs>(server_ClientDisconnected);
            server.PlaintextReceived +=
              new EventHandler<TcpDatagramReceivedEventArgs<string>>(server_PlaintextReceived);
            server.Start();
        }

        static void server_ClientConnected(object sender, TcpClientConnectedEventArgs e)
        {
            NowTcpClient = e.TcpClient;
            UpdateControl("C" + e.TcpClient.Client.RemoteEndPoint.ToString(), new EventArgs());
        }

        static void server_ClientDisconnected(object sender, TcpClientDisconnectedEventArgs e)
        {

            UpdateControl("D" + e.TcpClient.Client.RemoteEndPoint.ToString(), new EventArgs());
        }

        static void server_PlaintextReceived(object sender, TcpDatagramReceivedEventArgs<string> e)
        {
            if (e.Datagram != "Received")
            {
                RecTempString += e.Datagram;
                try
                {
                    while (true)
                    {
                        if (RecTempString.IndexOf("$$") >= 0)
                        {
                            int pPageLength = Convert.ToInt32(RecTempString.Substring(2, 4));

                            if (RecTempString.Length >= pPageLength)
                            {
                                switch (RecTempString.Substring(6, 2))
                                {
                                    case "AA":
                                        string vSendBack = "$$0014AA" + RecTempString.Substring(pPageLength - 6, 4);
                                        vSendBack += CheckSumString(vSendBack);
                                        UpdateControl("GPRS Rec:" + RecTempString.Substring(0, pPageLength), new EventArgs());
                                        SendBack(vSendBack);
                                        UpdateControl("GPRS SEND:" + vSendBack, new EventArgs());
                                        break;
                                    case "BB":
                                        string vSendBackHeartBeat = "$$0010BB01";
                                        UpdateControl("GPRS Rec:" + RecTempString.Substring(0, pPageLength), new EventArgs());
                                        SendBack(vSendBackHeartBeat);
                                        UpdateControl("GPRS SEND:" + vSendBackHeartBeat, new EventArgs());
                                        break;
                                    case "C0":
                                        int vOTABinPageNum = Convert.ToInt32(RecTempString.Substring(8, 4)) - 1;
                                        if (SendBack(FrimwareBinOTAArrayList[vOTABinPageNum]))
                                        {
                                            UpdateControl("O" + RecTempString.Substring(8, 4), new EventArgs());
                                        }
                                        break;
                                    case "AD":
                                        UpdateControl("L" + RecTempString.Substring(8), new EventArgs());
                                        break;
                                    case "CD":
                                        UpdateControl("E" + RecTempString.Substring(8), new EventArgs());
                                        break;
                                    default:
                                        UpdateControl("GPRS Rec:" + RecTempString.Substring(0, pPageLength), new EventArgs());
                                        break;
                                }
                                RecTempString = RecTempString.Substring(pPageLength, RecTempString.Length - pPageLength);
                            }
                            else
                            {
                                RecTempString = string.Empty;
                                break;
                            }
                        }
                        else
                        {
                            RecTempString = string.Empty;
                            break;
                        }
                    }
                }
                catch
                {
                }

            }
        }
        private void DisplayGPRSLog(string pDislay)
        {
            if (this.ShowLogCKb.Checked)
            {
                if (this.LogTb.InvokeRequired)
                {
                    SetGPRSTextCallback d = new SetGPRSTextCallback(DisplayGPRSLog);
                    this.Invoke(d, new object[] { pDislay });
                }
                else
                {
                    this.GPRSRectb.AppendText(pDislay);
                }
            }
        }
        private void UnitTestPorttb_KeyPress(object sender, KeyPressEventArgs e)
        {
            int kc = e.KeyChar;
            if ((kc < 48 || kc > 57) && kc != 8)
                e.Handled = true;
        }

        private void ServerPorttb_KeyPress(object sender, KeyPressEventArgs e)
        {
            int kc = e.KeyChar;
            if ((kc < 48 || kc > 57) && kc != 8)
                e.Handled = true;
        }
        private static string CheckSumString(string pStr)
        {
            byte[] vSouceByte = System.Text.Encoding.Default.GetBytes(pStr);
            int vResult = 0;
            for (int i = 0; i < vSouceByte.Length; i++)
            {
                vResult ^= vSouceByte[i];
            }
            return vResult.ToString("X2");
        }
        private static string CheckSumString(byte[] vSouceByte, int vLength)
        {
            int vResult = 0;
            for (int i = 0; i < vLength; i++)
            {
                vResult ^= vSouceByte[i];
            }
            return vResult.ToString("X2");
        }
        private void button11_Click(object sender, EventArgs e)
        {
            string vOTACmdStr = "$$" + (this.GPRSSendtb.Text.ToString().Length + 17).ToString("D4") + "CF" + this.Passwordtb.Text.ToString() + "," + this.GPRSSendtb.Text.ToString();
            vOTACmdStr += CheckSumString(vOTACmdStr);
            if (!SendBack(vOTACmdStr))
            {
                MessageBox.Show("链接已断开");
            }

        }
        private static bool SendBack(string pString)
        {
            try
            {
                if (NowTcpClient != null)
                {
                    server.Send(NowTcpClient, pString);
                    return true;
                }
            }
            catch
            {
                NowTcpClient = null;
                // MessageBox.Show("链接已断开");
            }
            return false;
        }
        private static bool SendBack(byte[] pDataGram)
        {
            try
            {
                if (NowTcpClient != null)
                {
                    //  UpdateControl(Encoding.ASCII.GetString(pDataGram), new EventArgs());
                    server.Send(NowTcpClient, pDataGram);
                    return true;
                }
            }
            catch
            {
                NowTcpClient = null;
                // MessageBox.Show("链接已断开");
            }
            return false;
        }
        private void OTAbt_Click(object sender, EventArgs e)
        {
            if (this.BinNametb.Text != "")
            {
                string vOTACmdStr = "$$0020CF" + this.Passwordtb.Text.ToString() + ",004";
                vOTACmdStr += CheckSumString(vOTACmdStr);
                this.GPRSpb.Value = 0;
                this.OTAlb.Text = "0%";
                if (!SendBack(vOTACmdStr))
                {
                    MessageBox.Show("链接已断开");
                }
            }
            else
            {
                MessageBox.Show("没有BIN文件");
            }
        }

        private void GPRSLoadConfigbt_Click(object sender, EventArgs e)
        {
            string vOTACmdStr = "$$0020CF" + this.Passwordtb.Text.ToString() + ",013";
            vOTACmdStr += CheckSumString(vOTACmdStr);
            if (!SendBack(vOTACmdStr))
            {
                MessageBox.Show("链接已断开");
            }
        }

        private void GPRSSaveConfigbt_Click(object sender, EventArgs e)
        {
            try
            {
                byte[] vConfigbyte = GetConfigBytes();
                if (vConfigbyte != null)
                {
                    int vBinSizeHave = vConfigbyte.Length;
                    byte[] vFrimewareBinArrayTemp = new byte[vBinSizeHave + 10];
                    int vBinSizeReadIndex = 0;
                    string xx = "$$" + (vBinSizeHave + 10).ToString("D4") + "CD";
                    byte[] vArrByte = System.Text.Encoding.ASCII.GetBytes(xx);

                    vFrimewareBinArrayTemp[0] = vArrByte[0];
                    vFrimewareBinArrayTemp[1] = vArrByte[1];

                    vFrimewareBinArrayTemp[2] = vArrByte[2];
                    vFrimewareBinArrayTemp[3] = vArrByte[3];
                    vFrimewareBinArrayTemp[4] = vArrByte[4];
                    vFrimewareBinArrayTemp[5] = vArrByte[5];
                    vFrimewareBinArrayTemp[6] = vArrByte[6];
                    vFrimewareBinArrayTemp[7] = vArrByte[7];             

                    for (int i = 0; i < vBinSizeHave; i++)
                    {
                        vFrimewareBinArrayTemp[i + 8] = vConfigbyte[vBinSizeReadIndex];
                        vBinSizeReadIndex += 1;
                    }
                    byte[] arrByte = System.Text.Encoding.ASCII.GetBytes(CheckSumString(vFrimewareBinArrayTemp, vBinSizeHave + 6));
                    vFrimewareBinArrayTemp[vBinSizeHave + 8] = arrByte[0];//Checksum
                    vFrimewareBinArrayTemp[vBinSizeHave + 9] = arrByte[1];//Checksum
                    if (!SendBack(vFrimewareBinArrayTemp))
                        MessageBox.Show("链接断开");
                }
            }
            catch
            {
            }
        }

        private void OilONbt_Click(object sender, EventArgs e)
        {
            string vOTACmdStr = "$$0024CF" + this.Passwordtb.Text.ToString() + ",016,A,1";
            vOTACmdStr += CheckSumString(vOTACmdStr);
            if (!SendBack(vOTACmdStr))
            {
                MessageBox.Show("链接已断开");
            }
        }

        private void OilOFFbt_Click(object sender, EventArgs e)
        {
            string vOTACmdStr = "$$0024CF" + this.Passwordtb.Text.ToString() + ",016,A,0";
            vOTACmdStr += CheckSumString(vOTACmdStr);
            if (!SendBack(vOTACmdStr))
            {
                MessageBox.Show("链接已断开");
            }
        }

        private void ClearGPRSRecbt_Click(object sender, EventArgs e)
        {
            this.GPRSRectb.Text = "";
        }
    }

}