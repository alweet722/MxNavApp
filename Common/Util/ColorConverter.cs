namespace NBNavApp.Common.Util;

internal class ColorConverter
{
    public static ushort ToRgb565(uint rgb888)
    {
        ushort B = (ushort)((rgb888 & 0x000000F8) >> 3);
        ushort G = (ushort)((rgb888 & 0x0000FC00) >> 5);
        ushort R = (ushort)((rgb888 & 0x00F80000) >> 8);
        return (ushort)(R | G | B);
    }

    public static uint ToRgb888(ushort rgb565)
    {
        uint B = (uint)(((rgb565 & 0x001F) << 3) | (rgb565 & 0x0007));
        uint G = (uint)(((rgb565 & 0x07E0) << 5) | ((rgb565 & 0x0060) << 3));
        uint R = (uint)(((rgb565 & 0xF800) << 8) | ((rgb565 & 0x3800) << 5));

        return (R | G | B);
    }
}
