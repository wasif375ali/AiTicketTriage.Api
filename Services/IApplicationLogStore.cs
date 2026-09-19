namespace AiTicketTriage.Api.Services
{
    public interface IApplicationLogStore
    {
        Task WriteAsync(
            string level,
            string message,
            CancellationToken cancellationToken = default);
    }
}
