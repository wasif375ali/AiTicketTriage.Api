using AiTicketTriage.Api.Contracts;
using AiTicketTriage.Api.Models;
using Google.GenAI;
using Google.GenAI.Types;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiTicketTriage.Api.Services
{
    public class GeminiTicketTriageService : IAiTicketTriageService
    {
        private readonly Client _client;
        private readonly string _model;
        private static readonly TimeSpan TotalAiTimeout = TimeSpan.FromSeconds(20);

        public GeminiTicketTriageService(Client client, IConfiguration configuration)
        {
            _client = client;

            _model = configuration["Gemini:Model"] ?? throw new InvalidOperationException("Gemini model is not configured.");
        }
        private static readonly JsonNode TicketTriageResponseSchema = JsonNode.Parse(
        """
        {
          "type": "object",
          "properties": {
            "summary": {
              "type": "string",
              "description": "A concise summary of the support ticket."
            },
            "category": {
              "type": "string",
              "enum": [
                  "Account Access",
                  "Application Error",
                  "Performance",
                  "User Interface",
                  "Billing",
                  "Other"
                ],
              "description": "The recommended category for the support ticket."
            },
            "priority": {
              "type": "string",
                "enum": [
                      "Low",
                      "Medium",
                      "High",
                      "Critical"
                    ],
              "description": "The recommended priority for the support ticket."
            },
            "suggestedAction": {
              "type": "string",
              "description": "A concise recommended next action."
            },
            "needsHumanReview": {
              "type": "boolean",
              "description": "Whether a human should review the recommendation before action."
            }
          },
          "required": [
            "summary",
            "category",
            "priority",
            "suggestedAction",
            "needsHumanReview"
          ],
          "additionalProperties": false
        }
        """
    )!;
        public async Task<TicketTriageResponse> TriageAsync(
            string subject,
            string description,
            CancellationToken cancellationToken = default)
        {


            using var totalTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            totalTimeoutCts.CancelAfter(TotalAiTimeout);

            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = new List<Part>
        {
            new()
            {
                 Text = """
                            You are a support-ticket triage assistant.

                               Analyze only the information present in the supplied ticket.
                               Do not invent missing facts.
                               Produce a concise business-friendly triage recommendation.
                               Recommend human review whenever uncertainty or business risk requires it.

                               Return the result according to the configured response schema.
                            """
            }
        }
                },

                ResponseMimeType = "application/json",
                ResponseJsonSchema = TicketTriageResponseSchema
            };

            var userInput = $"""
            Ticket Subject:
            {subject}

            Ticket Description:
            {description}
            """;


            var response = default(GenerateContentResponse);
            try
            {
                 response =
                       await _client.Models.GenerateContentAsync(
                           model: _model,
                           contents: userInput,
                           config: config,
                           cancellationToken: totalTimeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && totalTimeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Gemini triage exceeded the total timeout of " +
                    $"{TotalAiTimeout.TotalSeconds} seconds.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            var json =
                response.Candidates?[0]?.Content?.Parts?[0]?.Text
                ?? throw new InvalidOperationException(
                    "Gemini returned an empty response.");

            var modelOutput = JsonSerializer.Deserialize<TicketTriageModelOutput>(json);

            if (modelOutput is null)
            {
                throw new InvalidOperationException(
                    "Gemini structured output could not be deserialized.");
            }

            //c# business validator class
            TicketTriageOutputValidator.Validate(modelOutput);

            //if (modelOutput.Summary is null ||
            //    modelOutput.Category is null ||
            //    modelOutput.Priority is null ||
            //    modelOutput.SuggestedAction is null ||
            //    modelOutput.NeedsHumanReview is null)
            //{
            //    throw new InvalidOperationException(
            //        "Gemini returned incomplete structured output.");
            //}
            return new TicketTriageResponse
            {
                Summary = modelOutput.Summary!,
                Category = modelOutput.Category!,
                Priority = modelOutput.Priority!,
                SuggestedAction = modelOutput.SuggestedAction!,
                NeedsHumanReview = modelOutput.NeedsHumanReview.Value
            };
            //var humanReviewText =
            //    GetValue(rawText, "NEEDS_HUMAN_REVIEW:");

            //var needsHumanReview =
            //    !bool.TryParse(
            //        humanReviewText,
            //        out var parsedHumanReview)
            //    || parsedHumanReview;

            //return new TicketTriageResponse
            //{
            //    Summary = GetValue(rawText, "SUMMARY:"),
            //    Category = GetValue(rawText, "CATEGORY:"),
            //    Priority = GetValue(rawText, "PRIORITY:"),
            //    SuggestedAction = GetValue(rawText, "SUGGESTED_ACTION:"),
            //    NeedsHumanReview = needsHumanReview
            //};
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