class BleWriteFailedException : Exception
{
    public BleWriteFailedException() { }

    public BleWriteFailedException(string message) : base(message) { }

    public BleWriteFailedException(string? message, Exception? innerException) : base(message, innerException) { }
}