using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using EvCharge.Api.Domain;
using EvCharge.Api.Infrastructure.Errors;
using EvCharge.Api.Options;
using EvCharge.Api.Repositories;
using Microsoft.Extensions.Options;

namespace EvCharge.Api.Services
{
    public interface IPolicyService
    {
        // Booking create
        void EnsureWithinBookingHorizon(Station station, DateTime requestedLocalStart, DateTime utcNow);

        // Owner actions
        void EnsureOwnerModifyAllowed(Booking booking, DateTime utcNow);
        void EnsureOwnerCancelAllowed(Booking booking, DateTime utcNow);

        // Operator / sessions
        void EnsureEarliestCheckIn(Booking booking, DateTime utcNow);
        void EnsureCheckInWindow(Booking booking, DateTime utcNow); 
        DateTime ComputeLatestCheckInUtc(Booking booking);          
        bool IsNoShowEligible(Booking booking, DateTime utcNow);     
        // Station lifecycle
        Task EnsureStationCanDeactivateAsync(string stationId, CancellationToken ct);
    }

    public class PolicyService : IPolicyService
    {
        private readonly PolicyOptions _opts;
        private readonly IBookingRepository _bookings;

        public PolicyService(IOptions<PolicyOptions> opts, IBookingRepository bookings)
        {
            _opts = opts.Value;
            _bookings = bookings;
        }

        public void EnsureWithinBookingHorizon(Station station, DateTime requestedLocalStart, DateTime utcNow)
        {
            // Convert "now" to station local time for fair comparison
            var tzId = string.IsNullOrWhiteSpace(station.HoursTimezone) ? "UTC" : station.HoursTimezone;
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); } catch { tz = TimeZoneInfo.Utc; }

            var nowLocal = TimeZoneInfo.ConvertTime(utcNow, tz);

            // Past
            if (requestedLocalStart < nowLocal)
                throw new UpdateException("PolicyViolation.Horizon.PastDate", "Cannot create a booking in the past.");

            // Horizon limit (inclusive)
            var maxLocal = nowLocal.Date.AddDays(Math.Max(0, _opts.MaxBookingHorizonDays) + 1).AddTicks(-1); // end of last allowed day
            if (requestedLocalStart > maxLocal)
                throw new UpdateException("PolicyViolation.Horizon.TooFar",
                    $"Cannot book more than {_opts.MaxBookingHorizonDays} day(s) ahead.");
        }

        public void EnsureOwnerModifyAllowed(Booking booking, DateTime utcNow)
        {
            var lockStart = booking.SlotStartUtc.AddHours(-Math.Max(0, _opts.OwnerModifyLockHours));
            if (utcNow >= lockStart)
                throw new UpdateException("PolicyViolation.ModifyLockWindow",
                    $"Cannot modify within {_opts.OwnerModifyLockHours}h of start.");
        }

        public void EnsureOwnerCancelAllowed(Booking booking, DateTime utcNow)
        {
            var lockStart = booking.SlotStartUtc.AddHours(-Math.Max(0, _opts.OwnerModifyLockHours));
            if (utcNow >= lockStart)
                throw new UpdateException("PolicyViolation.CancelLockWindow",
                    $"Cannot cancel within {_opts.OwnerModifyLockHours}h of start.");
        }

        public void EnsureEarliestCheckIn(Booking booking, DateTime utcNow)
        {
            var earliest = booking.SlotStartUtc.AddMinutes(-Math.Max(0, _opts.EarliestCheckInMinutes));
            if (utcNow < earliest)
                throw new UpdateException("PolicyViolation.EarliestCheckIn",
                    $"Operator cannot check-in earlier than { _opts.EarliestCheckInMinutes } minutes before start.");
        }

        public async Task EnsureStationCanDeactivateAsync(string stationId, CancellationToken ct)
        {
            // If any future bookings are Approved or CheckedIn, block
            var now = DateTime.UtcNow;

            var approved = await _bookings.AdminListAsync(stationId, "Approved", now, null, ct);
            if (approved.Count > 0)
                throw new UpdateException("PolicyViolation.StationHasFutureBookings",
                    "Cannot deactivate station while future Approved bookings exist.");

            var checkedIn = await _bookings.AdminListAsync(stationId, "CheckedIn", now, null, ct);
            if (checkedIn.Count > 0)
                throw new UpdateException("PolicyViolation.StationHasFutureSessions",
                    "Cannot deactivate station while active/checked-in sessions exist.");
        }

        public void EnsureCheckInWindow(Booking booking, DateTime utcNow)
        {
            // Earliest
            EnsureEarliestCheckIn(booking, utcNow);

            // Latest: slot end + grace
            var latest = ComputeLatestCheckInUtc(booking);
            if (utcNow > latest)
                throw new UpdateException("PolicyViolation.LatestCheckInExceeded",
                    $"Check-in too late. Latest allowed was {LatestCheckInDisplay(booking)} (UTC).");
        }

        public DateTime ComputeLatestCheckInUtc(Booking booking)
        {
            var grace = Math.Max(0, _opts.LatestCheckInGraceMinutes);
            return booking.SlotStartUtc.AddMinutes(booking.SlotMinutes + grace);
        }

        public bool IsNoShowEligible(Booking booking, DateTime utcNow)
        {
            if (!string.Equals(booking.Status, "Approved", StringComparison.OrdinalIgnoreCase)) return false;
            return utcNow >= ComputeLatestCheckInUtc(booking);
        }

        // small helper (optional)
        private string LatestCheckInDisplay(Booking booking)
        {
            var dt = ComputeLatestCheckInUtc(booking);
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        }

    }
}
