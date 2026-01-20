namespace NBNavApp.Common.Messages;

public class ResetMessage : BleMessage
{
    public ResetMessage() : base(MessageType.RESET)
    {
    }
}
