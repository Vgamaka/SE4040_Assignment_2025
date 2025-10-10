using System.Security.Cryptography;
using System.Text;
using EvCharge.Api.Options;
using Microsoft.Extensions.Options;

namespace EvCharge.Api.Infrastructure.Qr
{
    public interface IQrTokenService
    {
        (string token, string hash) Create(string bookingId, string stationId, DateTime slotStartUtc);
    }

    public class QrTokenService : IQrTokenService
    {
        private readonly byte[] _key;

        public QrTokenService(IOptions<BookingOptions> opts, IOptions<JwtOptions> jwtOpts)
        {
            var s = opts.Value.QrSecret ?? jwtOpts.Value.Secret;
            _key = Encoding.UTF8.GetBytes(s);
        }

        public (string token, string hash) Create(string bookingId, string stationId, DateTime slotStartUtc)
        {
            var nonce = Guid.NewGuid().ToString("N");
            var payload = $"{nonce}|{bookingId}|{stationId}|{slotStartUtc.Ticks}";
            using var hmac = new HMACSHA256(_key);
            var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var tok = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{nonce}.{Convert.ToBase64String(sig)}"))
                .TrimEnd('=').Replace('+','-').Replace('/','_'); // base64url-ish

            // store hash of token (not token)
            using var sha = SHA256.Create();
            var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(tok))).ToLowerInvariant();
            return (tok, hash);
        }
    }
}
