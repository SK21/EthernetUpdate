using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace UpdateDemoApp
{
    public partial class Form1 : Form
    {
        //******************************************************************************
        // Intel Hex record format:
        //
        // Start code:  one character, ASCII colon ':'.
        // Byte count:  two hex digits, number of bytes (hex digit pairs) in data field.
        // Address:     four hex digits
        // Record type: two hex digits, 00 to 05, defining the meaning of the data field.
        // Data:        n bytes of data represented by 2n hex digits.
        // Checksum:    two hex digits, computed value used to verify record has no errors.
        //
        // Examples:
        //  :10 9D30 00 711F0000AD38000005390000F5460000 35
        //  :04 9D40 00 01480000 D6
        //  :00 0000 01 FF
        //******************************************************************************

        public clsTools Tls;
        public UDPcomm UDPupdate;
        private string cSubnet = "192.168.5.1";
        private bool FormEdited = false;
        private Dictionary<string, byte> hexindex = new Dictionary<string, byte>();
        private string IDname;
        private bool Initializing = true;
        private byte ModuleID = 0;
        private byte ModuleType = 0;          // 0 - Teensy AutoSteer, 1 - Teensy Rate
        private int TotalLines = 0;
        private bool UseDefault = false;

        public Form1()
        {
            InitializeComponent();
            Tls = new clsTools(this);
            UDPupdate = new UDPcomm(this, 29000, 29100, 9350, "UDPupdate");
        }

        public string Subnet
        {
            get { return cSubnet; }
            set
            {
                if (IPAddress.TryParse(value, out IPAddress IP))
                {
                    cSubnet = IP.ToString();
                    Tls.SaveProperty("SubNet", cSubnet);
                }
            }
        }

        public void CheckLines(byte[] data)
        {
            int lines = data[2] | (data[3] << 8) | (data[4] << 16) | (data[5] << 24);
            if (TotalLines == lines)
            {
                Tls.ShowHelp("Upload Success! Wait about 1 minute for the new firmware to be installed. The subnet may need to be updated after install.");
                UDPupdate.SendUDPMessage(new byte[] { 0x3a, 0x00, 0x00, 0x00, 0x06, 0xFA });
            }
            else
            {
                Tls.ShowHelp("Upload did not succeed");
                UDPupdate.SendUDPMessage(new byte[] { 0x3a, 0x00, 0x00, 0x00, 0x07, 0xF9 });
            }
        }

        public void DoUpdate(byte[] data)
        {
            try
            {
                if (data[2] == ModuleID && data[3] == 100)
                {
                    string filename = "";
                    timer1.Enabled = false;

                    if (UseDefault)
                    {
                        filename = Path.GetTempFileName();
                        switch (ModuleType)
                        {
                            default:
                                // autosteer
                                File.WriteAllBytes(filename, Properties.Resources.EthernetUpdateDemo_ino);
                                break;
                        }
                    }
                    else
                    {
                        filename = tbHexfile.Text;
                    }

                    if (File.Exists(filename))
                    {
                        progressBar.Value = 0;
                        int ExpectedLines = (int)new FileInfo(filename).Length / 45;

                        hexindex.Clear();
                        for (int i = 0; i <= 255; i++) hexindex.Add(i.ToString("X2"), (byte)i);
                        hexindex.Add("::", 0x3a);
                        TotalLines = 0;
                        using (StreamReader reader = new StreamReader(filename))
                        {
                            string line;
                            //read all the lines
                            DateTime prev = DateTime.Now;
                            DateTime start = DateTime.Now;
                            TimeSpan aa = new TimeSpan(TimeSpan.TicksPerMillisecond * 10);
                            int idx = 0;
                            while (true)
                            {
                                if (DateTime.Now - prev > aa)
                                {
                                    prev = DateTime.Now;
                                    line = "";
                                    for (int i = 0; i < 11; i++)
                                    {
                                        if (!reader.EndOfStream)
                                        {
                                            line += ":" + reader.ReadLine();
                                            idx++;
                                        }
                                    }
                                    lbCount.Text = idx.ToString();
                                    Application.DoEvents();

                                    UpdateProgress(idx * 100 / ExpectedLines);
                                    UDPupdate.SendUDPMessage(StrToByteArray(line));

                                    if (reader.EndOfStream)
                                    {
                                        break;
                                    }
                                }
                            }
                            TotalLines = idx;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Tls.WriteErrorLog(this.Text + "/DoUpdate " + ex.Message);
            }
            SetButtonUpload(true);
        }

        public byte[] StrToByteArray(string str)
        {
            List<byte> hexres = new List<byte>();
            for (int i = 0; i < str.Length; i += 2) hexres.Add(hexindex[str.Substring(i, 2)]);

            return hexres.ToArray();
        }

        private void bntOK_Click(object sender, EventArgs e)
        {
            if (FormEdited)
            {
                // save
                Tls.SaveProperty(IDname, tbID.Text);
                SetButtons(false);
                UpdateForm();
            }
            else
            {
                Close();
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            UseDefault = false;
            tbHexfile.Text = "";
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "hex files (*.hex)|*.hex|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 0;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    tbHexfile.Text = openFileDialog.FileName;
                    tbHexfile.Select(tbHexfile.Text.Length, 0);
                    tbHexfile.ScrollToCaret();
                }
            }
        }

        private void btnBrowse_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            string Message = "Search for new firmware (hex) files.";

            Tls.ShowHelp(Message, "Browse");
            hlpevent.Handled = true;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            btnUpload.Enabled = true;
        }

        private void btnDefault_Click(object sender, EventArgs e)
        {
            UseDefault = true;
            tbHexfile.Text = "Default file:  EthernetUpdateDemo.ino";
        }

        private void btnDefault_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            string Message = "Use the base firmware included in the app.";

            Tls.ShowHelp(Message, "Use default");
            hlpevent.Handled = true;
        }

        private void btnSendSubnet_Click(object sender, EventArgs e)
        {
            PGN33152 SetSubnet = new PGN33152(this);
            if (SetSubnet.Send(Subnet))
            {
                Tls.ShowHelp("New Subnet address sent.", "Subnet", 10000);
            }
            else
            {
                Tls.ShowHelp("New Subnet address not sent.", "Subnet", 10000);
            }
        }

        private void btnSendSubnet_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            string Message = "Change module subnet.";

            Tls.ShowHelp(Message, "Subnet");
            hlpevent.Handled = true;
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            try
            {
                PGN32800 BeginUpdate = new PGN32800(this);
                BeginUpdate.Send(ModuleID, ModuleType, ckOverwrite.Checked);
                timer1.Enabled = true;
                SetButtonUpload(false);
            }
            catch (Exception ex)
            {
                Tls.WriteErrorLog(this.Text + "/btnUpload " + ex.Message);
            }
        }

        private void btnUpload_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            string Message = "Upload to Teensy.";

            Tls.ShowHelp(Message, "Upload");
            hlpevent.Handled = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            UpdateForm();
        }

        private void cbEthernet_SelectedIndexChanged(object sender, EventArgs e)
        {
            Subnet = cbEthernet.Text;
            UpdateForm(false);
            UDPupdate.NetworkEP = Subnet;
        }

        private void frmFWTeensyNetwork_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                Tls.SaveFormData(this);
            }
        }

        private void frmFWTeensyNetwork_Load(object sender, EventArgs e)
        {
            Tls.LoadFormData(this);
            this.BackColor = Properties.Settings.Default.DayColour;

            UseDefault = true;
            switch (ModuleType)
            {
                default:
                    // autosteer
                    //tbHexfile.Text = "Default file version date: " + Tls.TeensyAutoSteerVersion();
                    this.Text = "Teensy Firmware Update";
                    IDname = "TeensySteerID";
                    break;
            }

            if (int.TryParse(Tls.LoadProperty(IDname), out int ID)) ModuleID = (byte)ID;

            UpdateForm();
            UDPupdate.NetworkEP = Subnet;
            UDPupdate.StartUDPServer();
            if (!UDPupdate.IsUDPSendConnected)
            {
                Tls.ShowHelp("UDPupdate failed to start.", "", 3000, true, true);
            }
            btnDefault_Click(sender, e);
        }

        private void groupBox1_Paint(object sender, PaintEventArgs e)
        {
            GroupBox box = sender as GroupBox;
            Tls.DrawGroupBox(box, e.Graphics, this.BackColor, Color.Black, Color.Blue);
        }

        private void LoadCombo()
        {
            // https://stackoverflow.com/questions/6803073/get-local-ip-address
            try
            {
                cbEthernet.Items.Clear();
                foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if ((item.NetworkInterfaceType == NetworkInterfaceType.Ethernet || item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) && item.OperationalStatus == OperationalStatus.Up)
                    {
                        foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                cbEthernet.Items.Add(ip.Address.ToString());
                            }
                        }
                    }
                }
                cbEthernet.SelectedIndex = cbEthernet.FindString(Subnet);
            }
            catch (Exception ex)
            {
                Tls.WriteErrorLog("frmModuleConfig/LoadCombo " + ex.Message);
            }
        }

        private void SetButtons(bool Edited)
        {
            if (!Initializing)
            {
                if (Edited)
                {
                    btnCancel.Enabled = true;
                    bntOK.Image = Properties.Resources.Save;
                }
                else
                {
                    btnCancel.Enabled = false;
                    bntOK.Image = Properties.Resources.bntOK_Image;
                }
                FormEdited = Edited;
            }
        }

        private void SetButtonUpload(bool Enabled)
        {
            btnUpload.Enabled = Enabled;
            btnDefault.Enabled = Enabled;
            btnBrowse.Enabled = Enabled;
        }

        private void tbHexfile_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            string Message = "Filename of firmware to upload to the Teensy.";

            Tls.ShowHelp(Message, "Firmware");
            hlpevent.Handled = true;
        }

        private void tbID_TextChanged(object sender, EventArgs e)
        {
            SetButtons(true);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Tls.ShowHelp("Could not connect to the module. Check connection or subnet.");
            timer1.Enabled = false;
            SetButtonUpload(true);
        }

        private void UpdateForm(bool UpdateCombo = true)
        {
            Initializing = true;
            tbID.Text = ModuleID.ToString();
            Initializing = false;
            if (UpdateCombo) LoadCombo();
        }

        private void UpdateProgress(int ProgressPercent)
        {
            if (ProgressPercent > 100) ProgressPercent = 100;
            if (ProgressPercent < 0) ProgressPercent = 0;

            progressBar.BeginInvoke(new Action(() => progressBar.Value = ProgressPercent));
        }
    }
}