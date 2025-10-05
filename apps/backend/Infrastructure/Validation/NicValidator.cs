using System.Text.RegularExpressions;

namespace EvCharge.Api.Infrastructure.Validation
{
    public static class NicValidator
    {
        // Accept 12-digit new NIC or 9 digits + V/X (case-insensitive) for old NIC
        private static readonly Regex NewNic = new(@"^\d{12}$", RegexOptions.Compiled);
        private static readonly Regex OldNic = new(@"^\d{9}[vVxX]$", RegexOptions.Compiled);

        public static bool IsValid(string nic)
        {
            if (string.IsNullOrWhiteSpace(nic)) return false;
            nic = nic.Trim();
            return NewNic.IsMatch(nic) || OldNic.IsMatch(nic);
        }
    }
}
