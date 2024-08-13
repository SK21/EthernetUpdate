#include <NativeEthernet.h>
#include <NativeEthernetUdp.h>

#include "FXUtil.h"		// read_ascii_line(), hex file support
extern "C" {
#include "FlashTxx.h"		// TLC/T3x/T4x/TMM flash primitives
}

#define ModuleID 0
#define InoType 0

// firmware update
EthernetUDP UpdateComm;
uint16_t UpdateReceivePort = 29100;
uint16_t UpdateSendPort = 29000;
uint32_t buffer_addr, buffer_size;
bool UpdateMode = false;
IPAddress DestinationIP(192, 168, 5, 255);

//******************************************************************************
// hex_info_t struct for hex record and hex file info
//******************************************************************************
typedef struct {  //
	char* data;   // pointer to array allocated elsewhere
	unsigned int addr;  // address in intel hex record
	unsigned int code;  // intel hex record type (0=data, etc.)
	unsigned int num; // number of data bytes in intel hex record

	uint32_t base;  // base address to be added to intel hex 16-bit addr
	uint32_t min;   // min address in hex file
	uint32_t max;   // max address in hex file

	int eof;    // set true on intel hex EOF (code = 1)
	int lines;    // number of hex records received
} hex_info_t;

static char data[16];// buffer for hex data

hex_info_t hex =
{ // intel hex info struct
  data, 0, 0, 0,        //   data,addr,num,code
  0, 0xFFFFFFFF, 0,     //   base,min,max,
  0, 0					//   eof,lines
};

void setup()
{
	Serial.begin(38400);

	// update firmware
	UpdateComm.begin(UpdateReceivePort);

	// ethernet 
	Serial.println("Starting Ethernet ...");
	IPAddress LocalIP(192, 168, 5, 103);
	static uint8_t LocalMac[] = { 0x0A,0x0B,0x42,0x0C,0x0D,0xFE };

	Ethernet.begin(LocalMac, 0);
	Ethernet.setLocalIP(LocalIP);

	delay(1500);
	if (Ethernet.linkStatus() == LinkON)
	{
		Serial.println("Ethernet Connected.");
	}
	else
	{
		Serial.println("Ethernet Not Connected.");
	}
	Serial.print("IP Address: ");
	Serial.println(Ethernet.localIP());
	Serial.println("");

	Serial.println("Finished setup.");
}

void loop()
{
	ReceiveUpdate();

}

bool GoodCRC(byte Data[], byte Length)
{
	byte ck = CRC(Data, Length - 1, 0);
	bool Result = (ck == Data[Length - 1]);
	return Result;
}

byte CRC(byte Chk[], byte Length, byte Start)
{
	byte Result = 0;
	int CK = 0;
	for (int i = Start; i < Length; i++)
	{
		CK += Chk[i];
	}
	Result = (byte)CK;
	return Result;
}

byte ParseModID(byte ID)
{
	// top 4 bits
	return ID >> 4;
}

