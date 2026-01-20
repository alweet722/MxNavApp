namespace NBNavApp.Common.Messages;

public class EtaMessage : BleMessage
{
    public TimeSpan Eta { get; }

    public EtaMessage(TimeSpan eta) : base(MessageType.ETA)
    {
        Eta = eta;

        Data[1] = (byte)Eta.Hours;
        Data[2] = (byte)Eta.Minutes;
    }
}
