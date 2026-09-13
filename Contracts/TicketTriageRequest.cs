using System.ComponentModel.DataAnnotations;

namespace AiTicketTriage.Api.Contracts
{
    public sealed class TicketTriageRequest
    {
        //public string Subject { get; init; } = string.Empty;

        //public string Description { get; init; } = string.Empty;

        [Required]
        [StringLength(
        200,
        MinimumLength = 3)]
        public string Subject { get; init; } = string.Empty;

        [Required]
        [StringLength(
            4000,
            MinimumLength = 10)]
        public string Description { get; init; } = string.Empty;

    }
}
