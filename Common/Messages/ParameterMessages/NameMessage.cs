using System.Text;

namespace NBNavApp.Common.Messages.ParameterMessages;

public class NameMessage : ParameterMessage
{
    /* 
        Message byte format:
        byte 0 = message type
        byte 1 = parameter type
        byte 2 = message count
        byte 3 = message index
        bytes 4 .. 7 = name ASCII bytes
    */

    public static NameMessage[] CreateNameMessages(string name)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(name);

        int chunkSize = 4;
        int c = (int)Math.Ceiling((double)bytes.Length / chunkSize);
        NameMessage[] msgs = new NameMessage[c];

        for (int i = 0; i < c; ++i)
        {
            NameMessage msg = new();
            msg.Data[2] = (byte)c;
            msg.Data[3] = (byte)i;

            int srcOffset = i * chunkSize;
            int copyLen = Math.Min(chunkSize, bytes.Length - srcOffset);

            Array.Copy(bytes, srcOffset, msg.Data, 4, copyLen);
            msgs[i] = msg;
        }
        return msgs;
    }

    public NameMessage() : base(ParameterType.NAME)
    { }
}
