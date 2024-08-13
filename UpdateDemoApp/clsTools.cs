using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;

namespace UpdateDemoApp
{
    public class clsTools
    {
        private static Hashtable ht;
        private string cAppName = "UpdateDemoApp";
        private string cPropertiesFile = "";
        private string cSettingsDir = "";
        private Form1 mf;

        public clsTools(Form1 CallingForm)
        {
            mf = CallingForm;
            CheckFolders();
        }

        public byte CRC(byte[] Data, int Length, byte Start = 0)
        {
            byte Result = 0;
            if (Length <= Data.Length)
            {
                int CK = 0;
                for (int i = Start; i < Length; i++)
                {
                    CK += Data[i];
                }
                Result = (byte)CK;
            }
            return Result;
        }

        public bool GoodCRC(byte[] Data, byte Start = 0)
        {
            bool Result = false;
            int Length = Data.Length;
            byte cr = CRC(Data, Length - 1, Start);
            Result = (cr == Data[Length - 1]);
            return Result;
        }

        public bool IsOnScreen(Form form, bool PutOnScreen = false)
        {
            // Create rectangle
            Rectangle formRectangle = new Rectangle(form.Left, form.Top, form.Width, form.Height);

            // Test
            bool IsOn = Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(formRectangle));

            if (!IsOn & PutOnScreen)
            {
                form.Top = 0;
                form.Left = 0;
            }

            return IsOn;
        }

        public void LoadFormData(Form Frm)
        {
            int Leftloc = 0;
            int.TryParse(LoadProperty(Frm.Name + ".Left"), out Leftloc);
            Frm.Left = Leftloc;

            int Toploc = 0;
            int.TryParse(LoadProperty(Frm.Name + ".Top"), out Toploc);
            Frm.Top = Toploc;

            IsOnScreen(Frm, true);
        }

        public string LoadProperty(string Key)
        {
            string Prop = "";
            if (ht.Contains(Key)) Prop = ht[Key].ToString();
            return Prop;
        }

        public void SaveFormData(Form Frm)
        {
            SaveProperty(Frm.Name + ".Left", Frm.Left.ToString());
            SaveProperty(Frm.Name + ".Top", Frm.Top.ToString());
        }

        public void SaveProperty(string Key, string Value)
        {
            bool Changed = false;
            if (ht.Contains(Key))
            {
                if (!ht[Key].ToString().Equals(Value))
                {
                    ht[Key] = Value;
                    Changed = true;
                }
            }
            else
            {
                ht.Add(Key, Value);
                Changed = true;
            }
            if (Changed) SaveProperties();
        }

        public void ShowHelp(string Message, string Title = "Help", int timeInMsec = 30000, bool LogError = false, bool Modal = false)
        {
            var Hlp = new frmHelp(mf, Message, Title, timeInMsec);
            if (Modal)
            {
                Hlp.ShowDialog();
            }
            else
            {
                Hlp.Show();
            }

            if (LogError) WriteErrorLog(Message);
        }

        public void WriteErrorLog(string strErrorText)
        {
            try
            {
                string FileName = cSettingsDir + "\\Error Log.txt";
                TrimFile(FileName);
                File.AppendAllText(FileName, DateTime.Now.ToString() + "  -  " + strErrorText + "\r\n\r\n");
            }
            catch (Exception)
            {
            }
        }

        private void CheckFolders()
        {
            try
            {
                // SettingsDir
                cSettingsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\" + cAppName;
                if (!Directory.Exists(cSettingsDir)) Directory.CreateDirectory(cSettingsDir);
                if (!File.Exists(cSettingsDir + "\\Example.con")) File.WriteAllBytes(cSettingsDir + "\\Example.con", Properties.Resources.Example);

                string FilesDir = Properties.Settings.Default.FilesDir;
                if (!Directory.Exists(FilesDir)) Properties.Settings.Default.FilesDir = cSettingsDir;

                OpenFile(Properties.Settings.Default.FileName);
            }
            catch (Exception)
            {
            }
        }

        private void LoadProperties(string path)
        {
            // property:  key=value  ex: "LastFile=Main.mdb"
            try
            {
                ht = new Hashtable();
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    if (line.Contains("=") && !string.IsNullOrEmpty(line.Split('=')[0]) && !string.IsNullOrEmpty(line.Split('=')[1]))
                    {
                        string[] splitText = line.Split('=');
                        ht.Add(splitText[0], splitText[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteErrorLog("Tools: LoadProperties: " + ex.Message);
            }
        }

        private void OpenFile(string NewFile)
        {
            try
            {
                string PathName = Path.GetDirectoryName(NewFile); // only works if file name present
                string FileName = Path.GetFileName(NewFile);
                if (FileName == "") PathName = NewFile;     // no file name present, fix path name
                if (Directory.Exists(PathName)) Properties.Settings.Default.FilesDir = PathName; // set the new files dir

                cPropertiesFile = Properties.Settings.Default.FilesDir + "\\" + FileName;
                if (!File.Exists(cPropertiesFile)) File.Create(cPropertiesFile).Dispose();
                LoadProperties(cPropertiesFile);
                Properties.Settings.Default.FileName = FileName;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                WriteErrorLog("Tools: OpenFile: " + ex.Message);
            }
        }

        private void SaveProperties()
        {
            try
            {
                string[] NewLines = new string[ht.Count];
                int i = -1;
                foreach (DictionaryEntry Pair in ht)
                {
                    i++;
                    NewLines[i] = Pair.Key.ToString() + "=" + Pair.Value.ToString();
                }
                if (i > -1) File.WriteAllLines(cPropertiesFile, NewLines);
            }
            catch (Exception)
            {
            }
        }

        private void TrimFile(string FileName, int MaxSize = 100000)
        {
            try
            {
                if (File.Exists(FileName))
                {
                    long FileSize = new FileInfo(FileName).Length;
                    if (FileSize > MaxSize)
                    {
                        // trim file
                        string[] Lines = File.ReadAllLines(FileName);
                        int Len = Lines.Length;
                        int St = (int)(Len * .1); // skip first 10% of old lines
                        string[] NewLines = new string[Len - St];
                        Array.Copy(Lines, St, NewLines, 0, Len - St);
                        File.Delete(FileName);
                        File.AppendAllLines(FileName, NewLines);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}