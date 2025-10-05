using System.Text.RegularExpressions;

namespace EvCharge.Api.Infrastructure.Validation
{
    public static class PhoneValidator
    {
        private static readonly Regex PhoneRx = new(@"^[\d\+\-\s]{6,20}$", RegexOptions.Compiled);

        public static bool IsValid(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return true; // optional
            return PhoneRx.IsMatch(phone.Trim());
        }
    }
}
