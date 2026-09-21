using System.Net;
using System.Net.Http;
using System.Text;

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
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                var status = (int)response.StatusCode;
                var retryable = IsRetryable(status) ? " retryable" : string.Empty;

                await WriteLogSafeAsync(
                    status >= 400 ? "Warning" : "Information",
                    $"Gemini HTTP {status} {response.StatusCode}{retryable} {request.Method} {request.RequestUri?.AbsolutePath}");

                return response;
            }
            catch (OperationCanceledException)
            {
                await WriteLogSafeAsync(
                    "Warning",
                    $"Gemini HTTP request timed out {request.Method} {request.RequestUri?.AbsolutePath} Outcome=Timeout");

                return CreateErrorResponse(
                    request,
                    HttpStatusCode.GatewayTimeout,
                    "The Gemini HTTP request timed out.");
            }
            catch (Exception)
            {
                await WriteLogSafeAsync(
                    "Warning",
                    $"Gemini HTTP provider error {request.Method} {request.RequestUri?.AbsolutePath} Outcome=ProviderFailed");

                return CreateErrorResponse(
                    request,
                    HttpStatusCode.BadGateway,
                    "The Gemini HTTP request failed.");
            }
        }

        private static HttpResponseMessage CreateErrorResponse(
            HttpRequestMessage request,
            HttpStatusCode statusCode,
            string message)
        {
            return new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
                ReasonPhrase = statusCode.ToString(),
                Content = new StringContent(message, Encoding.UTF8, "text/plain")
            };
        }

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

        private static bool IsRetryable(int status) =>
            status is 408 or 429 or >= 500;
    }
}
