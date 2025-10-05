using System.Text.RegularExpressions;

namespace EvCharge.Api.Infrastructure.Validation
{
    public static class PasswordValidator
    {
        // min 8 chars; at least one letter and one number
        private static readonly Regex Basic = new(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$", RegexOptions.Compiled);

        public static bool IsValid(string password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            return Basic.IsMatch(password);
        }
    }
}
