using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AiTicketTriage.Api.Exceptions
{
    public sealed class AiTriageExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetailsService;

        public AiTriageExceptionHandler(IProblemDetailsService problemDetailsService)
        {
            _problemDetailsService = problemDetailsService;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var problem = AiProblemDetailsFactory.Create(exception);
            httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

            try
            {
                var written = await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    Exception = exception,
                    ProblemDetails = problem
                });

                if (!written && !httpContext.Response.HasStarted)
                {
                    await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
                }
            }
            catch
            {
                if (!httpContext.Response.HasStarted)
                {
                    await httpContext.Response.WriteAsJsonAsync(problem, CancellationToken.None);
                }
            }

            return true;
        }
    }
}
