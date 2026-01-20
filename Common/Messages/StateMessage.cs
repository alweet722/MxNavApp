using NBNavApp.Common.Navigation;

namespace NBNavApp.Common.Messages;

public class StateMessage : BleMessage
{
    public RouteState Flag { get; }

    public StateMessage(RouteState flag) : base(MessageType.STATE)
    {
        Flag = flag;

        Data[1] = (byte)Flag;
    }
}
