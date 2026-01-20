namespace NBNavApp.Common.Messages.ParameterMessages;

public class ParameterMessage : BleMessage
{
    public enum ParameterType : byte
    {
        NAME = 0x01,
        COLOR = 0x02
    }

    public ParameterMessage(ParameterType parameterType) : base(MessageType.PARAMETER)
    {
        Data[1] = (byte)parameterType;
    }
}
