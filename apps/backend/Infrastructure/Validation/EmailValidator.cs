using System.Text.RegularExpressions;

namespace EvCharge.Api.Infrastructure.Validation
{
    public static class EmailValidator
    {
        private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public static bool IsValid(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            return EmailRx.IsMatch(email.Trim());
        }
    }
}
