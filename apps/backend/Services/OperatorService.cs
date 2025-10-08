using System.Globalization;
using System.Security.Claims;
using EvCharge.Api.Domain;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Infrastructure.Errors;
using EvCharge.Api.Repositories;

namespace EvCharge.Api.Services
{
    public interface IOperatorService
    {
        Task<List<OperatorInboxItem>> InboxAsync(string operatorNic, string dateYmd, CancellationToken ct);
        Task<BookingResponse> ScanAsync(OperatorScanRequest req, string operatorNic, ClaimsPrincipal principal, CancellationToken ct);
        Task<BookingResponse> ExceptionAsync(OperatorExceptionRequest req, string operatorNic, CancellationToken ct);
    }

    public class OperatorService : IOperatorService
    {
        private readonly IEvOwnerRepository _owners;
        private readonly IStationRepository _stations;
        private readonly IBookingRepository _bookings;
        private readonly ISessionService _sessions;
        private readonly IPolicyService _policy;

        public OperatorService(
            IEvOwnerRepository owners,
            IStationRepository stations,
            IBookingRepository bookings,
            ISessionService sessions,
            IPolicyService policy)
        
        {
            _owners   = owners;
            _stations = stations;
            _bookings = bookings;
            _sessions = sessions;
            _policy   = policy;
        }

public async Task<List<OperatorInboxItem>> InboxAsync(string operatorNic, string dateYmd, CancellationToken ct)
{
    // 0) Status set to include in the inbox
    // (order here does not affect output ordering; we sort by time at the end)
    string[] statuses =
    {
        "Pending",
        "Approved",
        "Rejected",
        "Cancelled",
        "NoShow",
        "Aborted",
        "Expired",
        "CheckedIn",
        "Completed"
    };

    // 1) Load operator and assigned stations
    var op = await _owners.GetByNicAsync(operatorNic, ct)
             ?? throw new AuthException("OperatorNotFound", "Operator account not found.");
    if (!op.Roles.Contains("Operator"))
        throw new AuthException("Forbidden", "Not an Operator account.");

    var stationIds = (op.OperatorStationIds ?? new List<string>())
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Distinct()
        .ToList();

    if (stationIds.Count == 0)
        return new List<OperatorInboxItem>();

    // 2) Parse inbox date (YYYY-MM-DD)
    if (!DateOnly.TryParseExact(dateYmd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        throw new ValidationException("InvalidDate", "date must be yyyy-MM-dd.");

    // 3) For each station, get timezone to compute UTC window for that local day
    var items = new List<OperatorInboxItem>();

    foreach (var sid in stationIds)
    {
        var st = await _stations.GetByIdAsync(sid, ct);
        if (st is null || !string.Equals(st.Status, "Active", StringComparison.OrdinalIgnoreCase))
            continue;

        var tzId = string.IsNullOrWhiteSpace(st.HoursTimezone) ? "UTC" : st.HoursTimezone;
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.Utc; }

        var localStart = DateTime.SpecifyKind(d.ToDateTime(new TimeOnly(0, 0)), DateTimeKind.Unspecified);
        var localEnd   = DateTime.SpecifyKind(d.AddDays(1).ToDateTime(new TimeOnly(0, 0)), DateTimeKind.Unspecified);
        var utcStart   = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var utcEnd     = TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);

        // 4) Query bookings for that station & day across all desired statuses
        // Use a dictionary to avoid any accidental duplicates by Id
        var byId = new Dictionary<string, OperatorInboxItem>(StringComparer.Ordinal);

        foreach (var status in statuses)
        {
            var list = await _bookings.AdminListAsync(sid, status, utcStart, utcEnd, ct);
            foreach (var b in list)
            {
                if (string.IsNullOrWhiteSpace(b.Id)) continue;

                // If the same booking somehow appears twice (shouldn't), keep first seen
                if (byId.ContainsKey(b.Id)) continue;

                byId[b.Id] = new OperatorInboxItem
                {
                    Id             = b.Id!,
                    BookingCode    = b.BookingCode,
                    StationId      = b.StationId,
                    SlotStartUtc   = b.SlotStartUtc,
                    SlotMinutes    = b.SlotMinutes,
                    Status         = b.Status,
                    OwnerNicMasked = MaskNic(b.OwnerNic),
                    SlotStartLocal = b.SlotStartLocal
                };
            }
        }

        items.AddRange(byId.Values);
    }

    // Order by start time ascending for "inbox" UX
    return items.OrderBy(x => x.SlotStartUtc).ToList();
}
        public async Task<BookingResponse> ScanAsync(OperatorScanRequest req, string operatorNic, ClaimsPrincipal principal, CancellationToken ct)
        {
            // Thin delegator to SessionService.CheckInAsync
            var sessReq = new SessionCheckInRequest { QrToken = req.QrToken, BookingId = req.BookingId };
            var res = await _sessions.CheckInAsync(sessReq, operatorNic, principal, ct);
            return res;
        }

public async Task<BookingResponse> ExceptionAsync(OperatorExceptionRequest req, string operatorNic, CancellationToken ct)
{
    // 1) Validate operator identity
    var op = await _owners.GetByNicAsync(operatorNic, ct)
             ?? throw new AuthException("OperatorNotFound", "Operator account not found.");
    if (!op.Roles.Contains("Operator"))
        throw new AuthException("Forbidden", "Not an Operator account.");

    // 2) Validate booking existence
    var b = await _bookings.GetByIdAsync(req.BookingId, ct)
            ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

    // 3) Ensure operator is assigned to this booking’s station
    var assigned = (op.OperatorStationIds ?? new List<string>()).Contains(b.StationId);
    if (!assigned)
        throw new AuthException("ForbiddenStation", "You are not assigned to this station.");

    // 4) Prepare update
    var nowUtc = DateTime.UtcNow;
    var reason = (req.Reason ?? string.Empty).Trim();
    var notes  = (req.Notes  ?? string.Empty).Trim();

    // 5) Apply status transitions
    if (reason.Equals("NoShow", StringComparison.OrdinalIgnoreCase))
    {
        if (!string.Equals(b.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            throw new UpdateException("InvalidState", "NoShow only from Approved.");

        if (!_policy.IsNoShowEligible(b, nowUtc))
            throw new UpdateException("TooEarly", "Cannot mark NoShow before slot end + grace.");

        b.Status = "NoShow";
    }
    else if (reason.Equals("Aborted", StringComparison.OrdinalIgnoreCase))
    {
        if (!string.Equals(b.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase))
            throw new UpdateException("InvalidState", "Aborted only from CheckedIn.");

        b.Status = "Aborted";
    }
    else if (reason.Equals("CustomerCancelOnSite", StringComparison.OrdinalIgnoreCase))
    {
        var okFromApproved   = string.Equals(b.Status, "Approved",   StringComparison.OrdinalIgnoreCase);
        var okFromCheckedIn  = string.Equals(b.Status, "CheckedIn",  StringComparison.OrdinalIgnoreCase);
        if (!(okFromApproved || okFromCheckedIn))
            throw new UpdateException("InvalidState", "Cancel-on-site allowed from Approved or CheckedIn.");

        b.Status = "Cancelled";
    }
    else
    {
        // Custom/unmapped reasons
        // Choose policy: either set a generic Exception status, or keep current status and only log.
        b.Status = "Exception"; // <— keep if you want a visible state for exceptions
        // If you prefer to only log, comment the above and leave status unchanged.
        // b.Status = b.Status;
    }

    // 6) Audit into Notes (since Booking has no Metadata)
    var auditLine = $"[Exception {nowUtc:O}] by {operatorNic} — Reason: {reason}"
                  + (string.IsNullOrWhiteSpace(notes) ? "" : $" | Notes: {notes}");
    b.Notes = string.IsNullOrWhiteSpace(b.Notes) ? auditLine : $"{b.Notes}\n{auditLine}";

    b.UpdatedAtUtc = nowUtc;
    b.UpdatedBy    = operatorNic;

    await _bookings.ReplaceAsync(b, ct);
    return ToResponse(b);
}

        // ---- helpers ----

        private static BookingResponse ToResponse(Booking b) => new BookingResponse
        {
            Id            = b.Id!,
            BookingCode   = b.BookingCode,
            OwnerNic      = b.OwnerNic,
            StationId     = b.StationId,
            Status        = b.Status,
            SlotStartLocal= b.SlotStartLocal,
            SlotStartUtc  = b.SlotStartUtc,
            SlotEndUtc    = b.SlotEndUtc,
            SlotMinutes   = b.SlotMinutes,
            Notes         = b.Notes,
            CreatedAtUtc  = b.CreatedAtUtc,
            UpdatedAtUtc  = b.UpdatedAtUtc,
            QrExpiresAtUtc= b.QrExpiresAtUtc
        };

        private static string MaskNic(string nic)
        {
            nic = (nic ?? "").Trim();
            if (nic.Length <= 4) return new string('*', Math.Max(0, nic.Length));
            var suffix = nic[^4..];
            return new string('*', Math.Max(0, nic.Length - 4)) + suffix;
        }
    }
}
