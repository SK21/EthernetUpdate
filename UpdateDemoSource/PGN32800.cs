using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpdateDemoApp
{
    public class PGN32800
    {
        // PGN32800, firmware update mode for Teensy 4.1
        //0		headerLo		32
        //1		headerHi		128
        //2		Module ID
        //3		Module Type		0-4
        //4     Command
        //          - overwrite module type
        //5		CRC

        private byte[] cData = new byte[6];
        private Form1 mf;

        public PGN32800(Form1 CalledFrom)
        {
            mf = CalledFrom;
            cData[0] = 32;
            cData[1] = 128;
        }

        public void Send(byte ModuleID, byte ModuleType, bool Overwrite = false)
        {
            cData[2] = ModuleID;
            cData[3] = ModuleType;
            if (Overwrite)
            {
                cData[4] = 1;
            }
            else
            {
                cData[4] = 0;
            }
            cData[5] = mf.Tls.CRC(cData, 5);
            mf.UDPupdate.SendUDPMessage(cData);
        }
    }
}