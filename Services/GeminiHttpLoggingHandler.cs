using System.Net;

namespace AiTicketTriage.Api.Services
{
    public sealed class GeminiHttpLoggingHandler : DelegatingHandler
    {
        private readonly IApplicationLogStore _logStore;

        public GeminiHttpLoggingHandler(
            HttpMessageHandler innerHandler,
            IApplicationLogStore logStore)
            : base(innerHandler)
        {
            _logStore = logStore;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            var retryable = IsRetryable(status) ? " retryable" : string.Empty;

            await _logStore.WriteAsync(
                status >= 400 ? "Warning" : "Information",
                $"Gemini HTTP {status} {response.StatusCode}{retryable} {request.Method} {request.RequestUri?.AbsolutePath}",
                CancellationToken.None);

            return response;
        }

        private static bool IsRetryable(int status) =>
            status is 408 or 429 or >= 500;
    }
}
