namespace AiTicketTriage.Api.Exceptions
{
    public sealed class AiContentBlockedException : Exception
    {
        public string? ProviderReason { get; }

        public AiContentBlockedException(
            string? providerReason = null)
            : base("The AI provider did not return usable content.")
        {
            ProviderReason = providerReason;
        }
    }
}
