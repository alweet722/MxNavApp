namespace NBNavApp.Common.Navigation.Messages;

public class ResetMessage : BleMessage
{
    public ResetMessage() : base(MessageType.RESET)
    {
    }
}
