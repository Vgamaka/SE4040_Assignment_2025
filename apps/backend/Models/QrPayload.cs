namespace backend.Models
{
    /// <summary>
    /// Request body sent by the Operator app to finalize a session after scanning a QR.
    /// The Token format expected by the backend is:
    ///   "booking:<ObjectId>;ts:<ticks>|<base64url_hmac>"
    /// </summary>
    public class QrFinalizeRequest
    {
        public string Token { get; set; } = string.Empty;
    }
}
