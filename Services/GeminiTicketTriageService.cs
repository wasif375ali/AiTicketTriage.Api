using AiTicketTriage.Api.Contracts;
using Google.GenAI;
using Google.GenAI.Types;

namespace AiTicketTriage.Api.Services
{
    public class GeminiTicketTriageService : IAiTicketTriageService
    {
        private readonly Client _client;
        private readonly string _model;

        public GeminiTicketTriageService(
            Client client,
            IConfiguration configuration)
        {
            _client = client;

            _model =
                configuration["Gemini:Model"]
                ?? throw new InvalidOperationException(
                    "Gemini model is not configured.");
        }

        public async Task<TicketTriageResponse> TriageAsync(
            string subject,
            string description,
            CancellationToken cancellationToken = default)
        {
            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts =
                    [
                        new Part
                        {
                            Text = """
                            You are a support ticket triage assistant.

                            Analyze only the supplied synthetic support ticket.

                            Return exactly these five labeled lines:

                            SUMMARY: concise ticket summary
                            CATEGORY: short business-friendly category
                            PRIORITY: Low, Medium, High, or Critical
                            SUGGESTED_ACTION: concise next action for a support engineer
                            NEEDS_HUMAN_REVIEW: true or false

                            Do not add markdown.
                            Do not add extra headings.
                            Do not add explanations before or after the five lines.

                            Your output is advisory only and must not be treated
                            as a final business decision.
                            """
                        }
                    ]
                }
            };

            var userInput = $"""
            Ticket Subject:
            {subject}

            Ticket Description:
            {description}
            """;

        

            var response =
                await _client.Models.GenerateContentAsync(
                    model: _model,
                    contents: userInput,
                    config: config,
                    cancellationToken: cancellationToken);
            await Task.Delay(
TimeSpan.FromSeconds(5),
cancellationToken);
            var rawText =
                response.Candidates?[0]?.Content?.Parts?[0]?.Text
                ?? throw new InvalidOperationException(
                    "Gemini returned an empty response.");

            var humanReviewText =
                GetValue(rawText, "NEEDS_HUMAN_REVIEW:");

            var needsHumanReview =
                !bool.TryParse(
                    humanReviewText,
                    out var parsedHumanReview)
                || parsedHumanReview;

            return new TicketTriageResponse
            {
                Summary = GetValue(rawText, "SUMMARY:"),
                Category = GetValue(rawText, "CATEGORY:"),
                Priority = GetValue(rawText, "PRIORITY:"),
                SuggestedAction = GetValue(rawText, "SUGGESTED_ACTION:"),
                NeedsHumanReview = needsHumanReview
            };
        }

        private static string GetValue(
            string text,
            string label)
        {
            var line = text
                .Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .FirstOrDefault(
                    x => x.StartsWith(
                        label,
                        StringComparison.OrdinalIgnoreCase));

            if (line is null)
            {
                return string.Empty;
            }

            return line[label.Length..].Trim();
        }
    }
}