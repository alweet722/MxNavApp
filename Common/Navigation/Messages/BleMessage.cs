namespace NBNavApp.Common.Navigation.Messages;

public enum MessageType : byte
{
    RESET = 0xFF,
    NAV = 0x01,
    ETA = 0x02,
    DIST = 0x04,
    STATE = 0x08,
    COLOR = 0x10
}

public class BleMessage
{
    public MessageType BleMessageType { get; }
    public byte[] Data { get; } = new byte[8];

    public BleMessage(MessageType messageType)
    {
        BleMessageType = messageType;
        Data[0] = (byte)BleMessageType;
    }
}
