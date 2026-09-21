using AiTicketTriage.Api.Contracts;
using AiTicketTriage.Api.Services;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace AiTicketTriage.Api.Controllers
{
    [ApiController]
    [Route("api/ai/tickets")]
    public class TicketTriageController : ControllerBase
    {
        private readonly IAiTicketTriageService _ticketTriageService;

        public TicketTriageController(
            IAiTicketTriageService ticketTriageService)
        {
            _ticketTriageService = ticketTriageService;
        }

        [HttpPost("triage")]
        [RequestTimeout("AiTriage")]
        public async Task<ActionResult<TicketTriageResponse>> Triage(
    [FromBody] TicketTriageRequest request,
    CancellationToken cancellationToken)
        {
            var result =
                await _ticketTriageService.TriageAsync(
                    request.Subject,
                    request.Description,
                    cancellationToken);

            if (result.Problem is not null)
            {
                return StatusCode(result.Problem.Status ?? 500, result.Problem);
            }

            return Ok(result.Value);
        }
    }
}
