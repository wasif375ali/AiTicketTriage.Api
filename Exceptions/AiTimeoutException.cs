namespace AiTicketTriage.Api.Exceptions
{
    public sealed class AiTimeoutException : Exception
    {
        public AiTimeoutException(string message)
            : base(message)
        {
        }

        public AiTimeoutException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
