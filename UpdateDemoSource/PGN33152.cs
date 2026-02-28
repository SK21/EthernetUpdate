using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpdateDemoApp
{
    public class PGN33152
    {
        //PGN33152, Subnet change, AGIO style
        //0     128     HeaderLo  
        //1     129     HeaderHi   
        //2     240     source      (127 from AGIO)
        //3     201     sub PGN     
        //4     5
        //5     201
        //6     201
        //7     IP 0
        //8     IP 1
        //9     IP 2

        private byte[] cData = new byte[10];
        private Form1 mf;
        public PGN33152(Form1 Main)
        {
            mf = Main;
            cData[0] = 128;
            cData[1] = 129;
            cData[2] = 240;
            cData[3] = 201;
            cData[4] = 5;
            cData[5] = 201;
            cData[6] = 201;
        }

        public bool Send(string EP)
        {
            string[] data = EP.Split('.');
            cData[7] = byte.Parse(data[0]);
            cData[8] = byte.Parse(data[1]);
            cData[9] = byte.Parse(data[2]);

            return mf.Tls.UDP_BroadcastPGN(cData);
        }
    }
}
