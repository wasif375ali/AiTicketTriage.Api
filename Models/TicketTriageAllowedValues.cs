namespace AiTicketTriage.Api.Models
{
    internal static class TicketTriageAllowedValues
    {
        public static readonly HashSet<string> Categories =
            new(StringComparer.Ordinal)
            {
            "Account Access",
            "Application Error",
            "Performance",
            "User Interface",
            "Billing",
            "Other"
            };

        public static readonly HashSet<string> Priorities =
            new(StringComparer.Ordinal)
            {
            "Low",
            "Medium",
            "High",
            "Critical"
            };
    }
}
