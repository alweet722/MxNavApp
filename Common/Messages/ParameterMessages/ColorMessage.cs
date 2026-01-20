using NBNavApp.Common.Util;

namespace NBNavApp.Common.Messages.ParameterMessages;

public class ColorMessage : ParameterMessage
{
    /* 
        Message byte format:
        byte 0 = message type
        byte 1 = parameter type
        byte 2 .. 3 = color as RGB565
    */

    public Color Color { get; }

    public ColorMessage(Color color) : base(ParameterType.COLOR)
    {
        Color = color;
        var rgb888 = Color.ToUint();
        var bytes = BitConverter.GetBytes(ColorConverter.ToRgb565(rgb888));

        for (int i = 0; i < bytes.Length; ++i)
        { Data[i + 2] = bytes[i]; }
    }
}
