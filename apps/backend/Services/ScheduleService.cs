using System.Globalization;
using EvCharge.Api.Domain;

namespace EvCharge.Api.Services
{
    public interface IScheduleService
    {
        List<(DateOnly date, int slots)> ComputeSevenDaySlotSummary(Station station, StationSchedule? schedule);
    }

    public class ScheduleService : IScheduleService
    {
        public List<(DateOnly date, int slots)> ComputeSevenDaySlotSummary(Station station, StationSchedule? schedule)
        {
            var tzId = string.IsNullOrWhiteSpace(station.HoursTimezone) ? "Asia/Colombo" : station.HoursTimezone;
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("UTC"); }

            var todayLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz).Date;
            var result = new List<(DateOnly, int)>();

            for (int i = 0; i < 7; i++)
            {
                var localDate = todayLocal.AddDays(i);
                var dateOnly = DateOnly.FromDateTime(localDate);

                int connectors = station.Connectors;
                if (schedule is not null)
                {
                    // exceptions - closed?
                    if (schedule.Exceptions.Any(e => e.Date == localDate.ToString("yyyy-MM-dd") && e.Closed))
                    {
                        result.Add((dateOnly, 0));
                        continue;
                    }

                    // capacity override
                    var ov = schedule.CapacityOverrides.FirstOrDefault(e => e.Date == localDate.ToString("yyyy-MM-dd"));
                    if (ov is not null && ov.Connectors > 0) connectors = ov.Connectors;
                }

                if (station.Status != "Active" || connectors <= 0)
                {
                    result.Add((dateOnly, 0));
                    continue;
                }

                var totalMinutes = 0;
                var dayRanges = GetRangesForDay(schedule?.Weekly, localDate.DayOfWeek);
                foreach (var r in dayRanges)
                {
                    if (!TimeSpan.TryParseExact(r.Start, "hh\\:mm", CultureInfo.InvariantCulture, out var start)) continue;
                    if (!TimeSpan.TryParseExact(r.End, "hh\\:mm", CultureInfo.InvariantCulture, out var end)) continue;
                    if (start >= end) continue;
                    totalMinutes += (int)(end - start).TotalMinutes;
                }

                var slotsPerConnector = station.DefaultSlotMinutes > 0 ? totalMinutes / station.DefaultSlotMinutes : 0;
                var slots = Math.Max(0, slotsPerConnector) * connectors;
                result.Add((dateOnly, slots));
            }

            return result;
        }

        private static List<DayTimeRange> GetRangesForDay(WeeklySchedule? weekly, DayOfWeek dow)
        {
            if (weekly is null) return new();
            return dow switch
            {
                DayOfWeek.Monday => weekly.Mon,
                DayOfWeek.Tuesday => weekly.Tue,
                DayOfWeek.Wednesday => weekly.Wed,
                DayOfWeek.Thursday => weekly.Thu,
                DayOfWeek.Friday => weekly.Fri,
                DayOfWeek.Saturday => weekly.Sat,
                DayOfWeek.Sunday => weekly.Sun,
                _ => new()
            };
        }
    }
}
