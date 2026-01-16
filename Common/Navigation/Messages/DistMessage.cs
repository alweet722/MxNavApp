namespace NBNavApp.Common.Navigation.Messages;

public class DistMessage : BleMessage
{
    public float Distance { get; }

    public DistMessage(float distance): base(MessageType.DIST)
    {
        Distance = distance;

        var bytes = BitConverter.GetBytes(Distance);
        for (int i = 0; i < bytes.Length; ++i)
        { Data[i + 1] = bytes[i]; }
    }
}
