public class OpenAiException : Exception
{
    public string ErrorCode { get; }
    public bool IsInsufficientFunds { get; }

    public OpenAiException(string message, string errorCode = null, bool isInsufficientFunds = false)
        : base(message)
    {
        ErrorCode = errorCode;
        IsInsufficientFunds = isInsufficientFunds;
    }
}