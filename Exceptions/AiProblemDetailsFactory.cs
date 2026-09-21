using Microsoft.AspNetCore.Mvc;

namespace AiTicketTriage.Api.Exceptions
{
    public static class AiProblemDetailsFactory
    {
        public static ProblemDetails Create(Exception exception)
        {
            var error = exception switch
            {
                AiContentBlockedException => new AiErrorContract(
                    StatusCodes.Status422UnprocessableEntity,
                    "AI triage requires manual review.",
                    "AI_CONTENT_BLOCKED",
                    HumanReviewRequired: true,
                    Retryable: false),

                AiOutputInvalidException => new AiErrorContract(
                    StatusCodes.Status502BadGateway,
                    "AI triage response could not be accepted.",
                    "AI_OUTPUT_INVALID",
                    HumanReviewRequired: true,
                    Retryable: false),

                AiProviderException or HttpRequestException => new AiErrorContract(
                    StatusCodes.Status502BadGateway,
                    "AI provider request failed.",
                    "AI_PROVIDER_ERROR",
                    HumanReviewRequired: true,
                    Retryable: true),

                AiTimeoutException or TimeoutException or TaskCanceledException
                    or OperationCanceledException => new AiErrorContract(
                    StatusCodes.Status504GatewayTimeout,
                    "AI triage timed out.",
                    "AI_TIMEOUT",
                    HumanReviewRequired: true,
                    Retryable: true),

                _ => new AiErrorContract(
                    StatusCodes.Status500InternalServerError,
                    "AI triage failed.",
                    "AI_UNEXPECTED_ERROR",
                    HumanReviewRequired: true,
                    Retryable: false)
            };

            return new ProblemDetails
            {
                Status = error.Status,
                Title = error.Title,
                Detail = exception.Message,
                Extensions =
                {
                    ["code"] = error.Code,
                    ["humanReviewRequired"] = error.HumanReviewRequired,
                    ["retryable"] = error.Retryable
                }
            };
        }

        private sealed record AiErrorContract(
            int Status,
            string Title,
            string Code,
            bool HumanReviewRequired,
            bool Retryable);
    }
}
