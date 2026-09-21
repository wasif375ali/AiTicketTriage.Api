using AiTicketTriage.Api.Contracts;
using AiTicketTriage.Api.Exceptions;
using AiTicketTriage.Api.Models;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiTicketTriage.Api.Services
{
    public class GeminiTicketTriageService : IAiTicketTriageService
    {
        private readonly Client _client;
        private readonly string _model;
        private readonly TimeSpan _totalAiTimeout;
        private readonly HttpOptions _httpOptions;
        // private readonly ILogger<GeminiTicketTriageService> _logger;
        private readonly IApplicationLogStore _logStore;
        private const string PromptVersion = "ticket-triage-prompt-002";
        public GeminiTicketTriageService(
            Client client,
            IConfiguration configuration,
            IApplicationLogStore logStore,
            HttpOptions httpOptions)
        // ILogger<GeminiTicketTriageService> logger)
        {
            _client = client;
            _logStore = logStore;
            _httpOptions = httpOptions;
            // _logger = logger;
            _model = configuration["Gemini:Model"] ?? string.Empty;
            _totalAiTimeout = TimeSpan.FromSeconds(
                configuration.GetValue<int?>("Ai:TriageTimeoutSeconds") ?? 90);
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
        public async Task<TicketTriageResult> TriageAsync(
            string subject,
            string description,
            CancellationToken cancellationToken = default)
        {
            try
            {
            if (string.IsNullOrWhiteSpace(_model))
            {
                return Fail(new AiOutputInvalidException("Gemini model is not configured."));
            }

            using var totalTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            totalTimeoutCts.CancelAfter(_totalAiTimeout);

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
                ResponseJsonSchema = TicketTriageResponseSchema,
                HttpOptions = _httpOptions
            };

            var userInput = $"""
                                Ticket Subject:
                                {subject}

                                Ticket Description:
                                {description}
                                """;


            var response = default(GenerateContentResponse);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                response =
                      await _client.Models.GenerateContentAsync(
                          model: _model,
                          contents: userInput,
                          config: config,
                          cancellationToken: totalTimeoutCts.Token);

                if (response.PromptFeedback?.BlockReason is { } promptBlockReason)
                {
                    await _logStore.WriteAsync(
                        "Warning",
                        "AI triage prompt was blocked. " +
                        $"PromptVersion={PromptVersion} " +
                        $"RequestedModel={_model} " +
                        $"ResponseId={response.ResponseId} " +
                        $"BlockReason={promptBlockReason} " +
                        "Outcome=ContentBlocked",
                        cancellationToken);

                    return Fail(new AiContentBlockedException(
                        promptBlockReason.ToString()));
                }

                var candidate = response.Candidates?.FirstOrDefault();

                var finishReason = candidate?.FinishReason;

                if (finishReason is { } reason && IsBlockedFinishReason(reason))
                {
                    await _logStore.WriteAsync(
                        "Warning",
                        "AI triage generation was blocked. " +
                        $"PromptVersion={PromptVersion} " +
                        $"RequestedModel={_model} " +
                        $"ResponseId={response.ResponseId} " +
                        $"FinishReason={reason} " +
                        "Outcome=ContentBlocked",
                        cancellationToken);

                    return Fail(new AiContentBlockedException(
                        reason.ToString()));
                }

            }

            catch (AiContentBlockedException ex)
            {
                return Fail(ex);
            }
            catch (AiTimeoutException ex)
            {
                return Fail(ex);
            }
            catch (AiProviderException ex)
            {
                return Fail(ex);
            }
            catch (AiOutputInvalidException ex)
            {
                return Fail(ex);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                if (IsTimeoutFailure(ex, totalTimeoutCts))
                {
                    await WriteLogSafeAsync(
                        "Warning",
                        "AI triage timed out. " +
                        $"PromptVersion={PromptVersion} " +
                        $"RequestedModel={_model} " +
                        $"AiCallLatencyMs={stopwatch.ElapsedMilliseconds} " +
                        "Outcome=Timeout");

                    return Fail(new AiTimeoutException(
                        $"Gemini triage exceeded the configured timeout of " +
                        $"{_totalAiTimeout.TotalSeconds} seconds.",
                        ex));
                }

                await WriteLogSafeAsync(
                    "Warning",
                    "AI provider call failed. " +
                    $"PromptVersion={PromptVersion} " +
                    $"RequestedModel={_model} " +
                    $"AiCallLatencyMs={stopwatch.ElapsedMilliseconds} " +
                    $"Error={ex.GetType().Name} " +
                    "Outcome=ProviderFailed");

                return Fail(new AiProviderException(
                    "The AI provider request failed.",
                    ex));
            }
            stopwatch.Stop();

            var aiCallLatencyMs = stopwatch.ElapsedMilliseconds;

            //toekn usage metadata
            var usage = response.UsageMetadata;
            var promptTokens = usage?.PromptTokenCount;
            var outputTokens = usage?.CandidatesTokenCount;
            var totalTokens = usage?.TotalTokenCount;

            //model tracability fields
            var actualModelVersion = response.ModelVersion;
            var providerResponseId = response.ResponseId;

            var json = response.Candidates?[0]?.Content?.Parts?[0]?.Text;
            if (string.IsNullOrWhiteSpace(json))
            {
                return Fail(new AiOutputInvalidException(
                    "Gemini returned no usable structured output."));
            }

            //var modelOutput = JsonSerializer.Deserialize<TicketTriageModelOutput>(json);

            TicketTriageModelOutput? modelOutput;

            try
            {
                modelOutput =
                    JsonSerializer.Deserialize<TicketTriageModelOutput>(
                        json);
            }
            catch (JsonException ex)
            {
                return Fail(new AiOutputInvalidException(
                    "Gemini output could not be deserialized.",
                    ex));
            }

            if (modelOutput is null)
            {
                return Fail(new AiOutputInvalidException(
                    "Gemini output deserialized to null."));
            }

            //c# business validator class
            try
            {
                TicketTriageOutputValidator.Validate(modelOutput);
                // _logger.LogInformation(
                //     "AI triage completed. " +
                //     "PromptVersion={PromptVersion} " +
                //     "RequestedModel={RequestedModel} " +
                //     "ActualModelVersion={ActualModelVersion} " +
                //     "ResponseId={ResponseId} " +
                //     "AiCallLatencyMs={AiCallLatencyMs} " +
                //     "PromptTokens={PromptTokens} " +
                //     "OutputTokens={OutputTokens} " +
                //     "TotalTokens={TotalTokens} " +
                //     "ValidationResult={ValidationResult} " +
                //     "Outcome={Outcome}",
                //     PromptVersion,
                //     "gemini-3.5-flash-lite",
                //     actualModelVersion,
                //     providerResponseId,
                //     aiCallLatencyMs,
                //     promptTokens,
                //     outputTokens,
                //     totalTokens,
                //     "Passed",
                //     "Success");
                await _logStore.WriteAsync(
                    "Information",
                    "AI triage completed. " +
                    $"PromptVersion={PromptVersion} " +
                    "RequestedModel=gemini-3.5-flash-lite " +
                    $"ActualModelVersion={actualModelVersion} " +
                    $"ResponseId={providerResponseId} " +
                    $"AiCallLatencyMs={aiCallLatencyMs} " +
                    $"PromptTokens={promptTokens} " +
                    $"OutputTokens={outputTokens} " +
                    $"TotalTokens={totalTokens} " +
                    "ValidationResult=Passed " +
                    "Outcome=Success",
                    cancellationToken);
            }
            catch (AiOutputInvalidException ex)
            {
                // _logger.LogWarning(
                //     "AI triage validation failed. " +
                //     "PromptVersion={PromptVersion} " +
                //     "RequestedModel={RequestedModel} " +
                //     "ActualModelVersion={ActualModelVersion} " +
                //     "ResponseId={ResponseId} " +
                //     "AiCallLatencyMs={AiCallLatencyMs} " +
                //     "PromptTokens={PromptTokens} " +
                //     "OutputTokens={OutputTokens} " +
                //     "TotalTokens={TotalTokens} " +
                //     "ValidationResult={ValidationResult} " +
                //     "Outcome={Outcome}",
                //     PromptVersion,
                //     "gemini-3.5-flash-lite",
                //     actualModelVersion,
                //     providerResponseId,
                //     aiCallLatencyMs,
                //     promptTokens,
                //     outputTokens,
                //     totalTokens,
                //     "Failed",
                //     "ValidationFailed");
                await _logStore.WriteAsync(
                    "Warning",
                    "AI triage validation failed. " +
                    $"PromptVersion={PromptVersion} " +
                    "RequestedModel=gemini-3.5-flash-lite " +
                    $"ActualModelVersion={actualModelVersion} " +
                    $"ResponseId={providerResponseId} " +
                    $"AiCallLatencyMs={aiCallLatencyMs} " +
                    $"PromptTokens={promptTokens} " +
                    $"OutputTokens={outputTokens} " +
                    $"TotalTokens={totalTokens} " +
                    "ValidationResult=Failed " +
                    "Outcome=ValidationFailed",
                    cancellationToken);

                return Fail(ex);
            }

            //if (modelOutput.Summary is null ||
            //    modelOutput.Category is null ||
            //    modelOutput.Priority is null ||
            //    modelOutput.SuggestedAction is null ||
            //    modelOutput.NeedsHumanReview is null)
            //{
            //    throw new AiOutputInvalidException(
            //        "Gemini returned incomplete structured output.");
            //}
            return TicketTriageResult.Success(new TicketTriageResponse
            {
                Summary = modelOutput.Summary!,
                Category = modelOutput.Category!,
                Priority = modelOutput.Priority!,
                SuggestedAction = modelOutput.SuggestedAction!,
                NeedsHumanReview = modelOutput.NeedsHumanReview.Value
            });
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
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

        private static bool IsBlockedFinishReason(FinishReason reason)
        {
            return
                reason == FinishReason.Safety ||
                reason == FinishReason.Blocklist ||
                reason == FinishReason.ProhibitedContent ||
                reason == FinishReason.Spii ||
                reason == FinishReason.Recitation ||
                reason == FinishReason.Language;
        }

        private static TicketTriageResult Fail(Exception exception) =>
            TicketTriageResult.Failure(AiProblemDetailsFactory.Create(exception));

        private async Task WriteLogSafeAsync(string level, string message)
        {
            try
            {
                using var logCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _logStore.WriteAsync(level, message, logCts.Token);
            }
            catch
            {
            }
        }

        private static bool IsTimeoutFailure(
            Exception exception,
            CancellationTokenSource totalTimeoutCts)
        {
            if (exception is TimeoutException or AiTimeoutException)
            {
                return true;
            }

            if (exception is not OperationCanceledException)
            {
                return false;
            }

            return totalTimeoutCts.IsCancellationRequested
                || exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }

    }
}