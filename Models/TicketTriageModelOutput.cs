using System.Text.Json.Serialization;

namespace AiTicketTriage.Api.Models
{
    internal sealed class TicketTriageModelOutput
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; init; }

        [JsonPropertyName("category")]
        public string? Category { get; init; }

        [JsonPropertyName("priority")]
        public string? Priority { get; init; }

        [JsonPropertyName("suggestedAction")]
        public string? SuggestedAction { get; init; }

        [JsonPropertyName("needsHumanReview")]
        public bool? NeedsHumanReview { get; init; }
    }
}
