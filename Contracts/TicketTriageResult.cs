using Microsoft.AspNetCore.Mvc;

namespace AiTicketTriage.Api.Contracts
{
    public sealed class TicketTriageResult
    {
        public TicketTriageResponse? Value { get; init; }

        public ProblemDetails? Problem { get; init; }

        public static TicketTriageResult Success(TicketTriageResponse value) =>
            new() { Value = value };

        public static TicketTriageResult Failure(ProblemDetails problem) =>
            new() { Problem = problem };
    }
}
