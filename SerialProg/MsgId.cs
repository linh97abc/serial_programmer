namespace SerialProg;

enum MessageId : byte
{
	RESET_SESSION = 0,
	NACK = 1,
	PING = 2,
	READ_STATUS = 5,
	GET_IMG_INFO = 6,
	BEGIN_UP_FILE = 10,
	DATA_FILE = 11,
	PROG_FLASH = 12,
}