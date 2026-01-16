namespace NBNavApp.Common.Navigation.Messages;

public class NavMessage : BleMessage
{
    public Instruction ManeuverType { get; }
    public uint DistMeters { get; }
    public byte Exit { get; } = 0;

    public NavMessage(Instruction maneuverType, uint distMeters, byte exit) : base(MessageType.NAV)
    {
        ManeuverType = maneuverType;
        DistMeters = distMeters;
        Exit = exit;

        var bytes = BitConverter.GetBytes(distMeters);

        Data[1] = (byte)ManeuverType;
        for (int i = 0; i < bytes.Length; ++i)
        { Data[i + 2] = bytes[i]; }
        Data[6] = Exit;
    }
}
