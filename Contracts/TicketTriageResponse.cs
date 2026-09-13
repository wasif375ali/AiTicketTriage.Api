namespace AiTicketTriage.Api.Contracts
{
    public sealed class TicketTriageResponse
    {
        public string Summary { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public string Priority { get; init; } = string.Empty;

        public string SuggestedAction { get; init; } = string.Empty;

        public bool NeedsHumanReview { get; init; }
    }
}
