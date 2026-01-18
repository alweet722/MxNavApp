namespace NBNavApp.Common.Navigation.Messages;

public class DistMessage : BleMessage
{
    public uint Distance { get; }

    public DistMessage(uint distance): base(MessageType.DIST)
    {
        Distance = distance;

        var bytes = BitConverter.GetBytes(Distance);
        for (int i = 0; i < bytes.Length; ++i)
        { Data[i + 1] = bytes[i]; }
    }
}
