namespace AiTicketTriage.Api.Exceptions
{
    public sealed class AiOutputInvalidException : Exception
    {
        public AiOutputInvalidException(string message)
            : base(message)
        {
        }

        public AiOutputInvalidException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
