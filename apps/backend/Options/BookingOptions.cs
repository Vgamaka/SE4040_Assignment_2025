namespace EvCharge.Api.Options
{
    public class BookingOptions
    {
        public string ApprovalMode { get; set; } = "Manual"; // Manual|Auto
        public int CancelCutoffMinutes { get; set; } = 30;
        public int QrExpiryAfterStartMinutes { get; set; } = 15;
        public string? QrSecret { get; set; } // optional; fallback to Jwt.Secret if null
    }
}
