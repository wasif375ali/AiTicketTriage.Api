using AiTicketTriage.Api.Contracts;

namespace AiTicketTriage.Api.Services
{
    public interface IAiTicketTriageService
    {
        Task<TicketTriageResult> TriageAsync(
            string subject,
            string description,
            CancellationToken cancellationToken = default);
    }
}
