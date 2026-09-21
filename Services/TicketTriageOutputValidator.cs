using AiTicketTriage.Api.Exceptions;
using AiTicketTriage.Api.Models;

namespace AiTicketTriage.Api.Services
{
    internal static class TicketTriageOutputValidator
    {
        public static void Validate(TicketTriageModelOutput output)
        {
            ArgumentNullException.ThrowIfNull(output);

            if (string.IsNullOrWhiteSpace(output.Summary))
            {
                throw new AiOutputInvalidException(
                    "AI output contains an empty summary.");
            }

            if (string.IsNullOrWhiteSpace(output.Category))
            {
                throw new AiOutputInvalidException(
                    "AI output contains an empty category.");
            }

            if (!TicketTriageAllowedValues.Categories.Contains(output.Category))
            {
                throw new AiOutputInvalidException(
                    $"AI output contains unsupported category '{output.Category}'.");
            }

            if (string.IsNullOrWhiteSpace(output.Priority))
            {
                throw new AiOutputInvalidException(
                    "AI output contains an empty priority.");
            }

            if (!TicketTriageAllowedValues.Priorities.Contains(output.Priority))
            {
                throw new AiOutputInvalidException(
                    $"AI output contains unsupported priority '{output.Priority}'.");
            }

            if (string.IsNullOrWhiteSpace(output.SuggestedAction))
            {
                throw new AiOutputInvalidException(
                    "AI output contains an empty suggested action.");
            }

            if (output.NeedsHumanReview is null)
            {
                throw new AiOutputInvalidException(
                    "AI output does not contain needsHumanReview.");
            }
        }
    }
}