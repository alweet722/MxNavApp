namespace NBNavApp.Common.Navigation.Messages;

public class StateMessage : BleMessage
{
    public RouteState Flag { get; }

    public StateMessage(RouteState flag) : base(MessageType.STATE)
    {
        Flag = flag;

        Data[1] = (byte)Flag;
    }
}
