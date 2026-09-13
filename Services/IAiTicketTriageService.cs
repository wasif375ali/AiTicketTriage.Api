using AiTicketTriage.Api.Contracts;

namespace AiTicketTriage.Api.Services
{
    public interface IAiTicketTriageService
    {
        Task<TicketTriageResponse> TriageAsync(
       string subject,
       string description,
       CancellationToken cancellationToken = default);
    }
}
