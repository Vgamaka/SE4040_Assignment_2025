using System.Globalization;

namespace EvCharge.Api.Infrastructure.Validation
{
    public static class ScheduleValidator
    {
        public static bool IsValidTime(string t) =>
            TimeSpan.TryParseExact(t, "hh\\:mm", CultureInfo.InvariantCulture, out _)
            || TimeSpan.TryParseExact(t, "h\\:mm", CultureInfo.InvariantCulture, out _);

        public static bool IsValidRange(string start, string end)
        {
            if (!IsValidTime(start) || !IsValidTime(end)) return false;
            var s = TimeSpan.Parse(start);
            var e = TimeSpan.Parse(end);
            return s < e;
        }

        public static bool ValidSlotMinutes(int m) => new[] { 30, 45, 60, 90, 120 }.Contains(m);

        public static bool ValidType(string? t) => t is "AC" or "DC";

        public static bool ValidPricingModel(string? m) => m is "flat" or "hourly" or "kwh";
    }
}
