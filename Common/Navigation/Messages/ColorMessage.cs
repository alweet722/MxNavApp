namespace NBNavApp.Common.Navigation.Messages;

public class ColorMessage : BleMessage
{
    public ushort Color { get; }

    public ColorMessage() : base(MessageType.COLOR)
    {
        var bytes = BitConverter.GetBytes(Color);

        for (int i = 0; i < bytes.Length; ++i)
        { Data[i + 1] = bytes[i]; }
    }
}
