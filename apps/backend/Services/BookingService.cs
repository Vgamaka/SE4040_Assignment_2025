// using System.Globalization;
// using System.Linq;
// using EvCharge.Api.Domain;
// using EvCharge.Api.Domain.DTOs;
// using EvCharge.Api.Infrastructure.Errors;
// using EvCharge.Api.Infrastructure.Qr;
// using EvCharge.Api.Options;
// using EvCharge.Api.Repositories;
// using Microsoft.Extensions.Options;

// namespace EvCharge.Api.Services
// {
//     public interface IBookingService
//     {
//         Task<BookingResponse> CreateAsync(string ownerNic, BookingCreateRequest req, CancellationToken ct);
//         Task<List<BookingListItem>> GetMineAsync(string ownerNic, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
//         Task<BookingResponse> GetByIdAsync(string id, string actorNic, bool isStaff, CancellationToken ct);
//         Task<BookingResponse> UpdateAsync(string id, string ownerNic, BookingUpdateRequest req, CancellationToken ct);
//         Task<BookingResponse> CancelAsync(string id, string ownerNic, CancellationToken ct);

//         Task<List<BookingListItem>> AdminListAsync(string? stationId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
//         Task<BookingApprovalResponse> ApproveAsync(string id, string staffNic, CancellationToken ct);
//         Task<BookingResponse> RejectAsync(string id, string staffNic, CancellationToken ct);

//         // NEW: allow Owner (or staff) to re-issue a QR token after approval
//         Task<BookingApprovalResponse> IssueQrAsync(string id, string actorNic, bool isStaff, CancellationToken ct);
//     }

//     public class BookingService : IBookingService
//     {
//         private readonly IBookingRepository _repo;
//         private readonly IStationRepository _stations;
//         private readonly IScheduleService _schedules;
//         private readonly IQrTokenService _qr;
//         private readonly BookingOptions _opts;

//         public BookingService(
//             IBookingRepository repo,
//             IStationRepository stations,
//             IScheduleService schedules,
//             IQrTokenService qr,
//             IOptions<BookingOptions> opts)
//         {
//             _repo = repo;
//             _stations = stations;
//             _schedules = schedules;
//             _qr = qr;
//             _opts = opts.Value;
//         }

//         public async Task<BookingResponse> CreateAsync(string ownerNic, BookingCreateRequest req, CancellationToken ct)
//         {
//             var st = await _stations.GetByIdAsync(req.StationId, ct)
//                      ?? throw new ValidationException("InvalidStation", "Station not found.");
//             if (st.Status != "Active")
//                 throw new UpdateException("StationNotActive", "Station is not active.");

//             if (req.Minutes != st.DefaultSlotMinutes)
//                 throw new ValidationException("InvalidMinutes", $"Minutes must be {st.DefaultSlotMinutes}.");
//             if (!IsValidTime(req.StartTime))
//                 throw new ValidationException("InvalidTime", "StartTime must be HH:mm.");
//             if (!DateOnly.TryParseExact(req.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
//                 throw new ValidationException("InvalidDate", "LocalDate must be yyyy-MM-dd.");

//             var sch = await _stations.GetScheduleAsync(st.Id!, ct);
//             var localStart = ParseLocal(st.HoursTimezone, d, req.StartTime);
//             var localEnd = localStart.AddMinutes(req.Minutes);
//             if (!IsWithinSchedule(localStart, localEnd, sch))
//                 throw new ValidationException("InvalidScheduleRange", "Requested time is outside station open hours.");

//             var capacity = ResolveCapacityForDate(st, sch, localStart.Date);
//             if (capacity <= 0)
//                 throw new UpdateException("StationClosed", "Station is closed on that date.");

//             var startUtc = ToUtc(localStart, st.HoursTimezone);
//             var endUtc = ToUtc(localEnd, st.HoursTimezone);

//             await _repo.EnsureIndexesAsync(ct);
//             await _repo.EnsureInventoryDocAsync(st.Id!, startUtc, endUtc, capacity, ct);

//             var reserved = await _repo.TryReserveAsync(st.Id!, startUtc, ct);
//             if (!reserved)
//                 throw new UpdateException("CapacityFull", "The selected slot is full.");

//             var now = DateTime.UtcNow;
//             var booking = new Booking
//             {
//                 BookingCode = MakeCode(),
//                 OwnerNic = ownerNic,
//                 StationId = st.Id!,
//                 SlotStartUtc = startUtc,
//                 SlotEndUtc = endUtc,
//                 SlotStartLocal = $"{d:yyyy-MM-dd}T{req.StartTime}",
//                 SlotMinutes = req.Minutes,
//                 Status = "Pending",
//                 Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
//                 CreatedAtUtc = now
//             };

//             // Auto-approve policy (per station or global)
//             var auto = st.AutoApproveEnabled || string.Equals(_opts.ApprovalMode, "Auto", StringComparison.OrdinalIgnoreCase);
//             if (auto)
//             {
//                 var (_, hashTemp) = _qr.Create("temp", st.Id!, startUtc); // temporary until we have ID
//                 booking.Status = "Approved";
//                 booking.ApprovedAtUtc = now;
//                 booking.ApprovedBy = "system-auto";
//                 booking.QrTokenHash = hashTemp;
//                 booking.QrExpiresAtUtc = booking.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             }

//             booking.Id = await _repo.CreateAsync(booking, ct);

//             // If auto, re-issue with real ID (hash changes; acceptable)
//             if (auto)
//             {
//                 var (_, hash) = _qr.Create(booking.Id!, st.Id!, startUtc);
//                 booking.QrTokenHash = hash;
//                 await _repo.ReplaceAsync(booking, ct);
//                 return ToResponse(booking);
//             }

//             return ToResponse(booking);
//         }

//         public async Task<List<BookingListItem>> GetMineAsync(string ownerNic, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
//         {
//             var list = await _repo.GetMineAsync(ownerNic, status, fromUtc, toUtc, ct);
//             return list.Select(b => new BookingListItem
//             {
//                 Id = b.Id!,
//                 BookingCode = b.BookingCode,
//                 StationId = b.StationId,
//                 Status = b.Status,
//                 SlotStartUtc = b.SlotStartUtc,
//                 SlotMinutes = b.SlotMinutes
//             }).ToList();
//         }

//         public async Task<BookingResponse> GetByIdAsync(string id, string actorNic, bool isStaff, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
//             if (!isStaff && !string.Equals(b.OwnerNic, actorNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");
//             return ToResponse(b);
//         }

//         public async Task<BookingResponse> UpdateAsync(string id, string ownerNic, BookingUpdateRequest req, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

//             if (!string.Equals(b.OwnerNic, ownerNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");

//             if (b.Status is not ("Pending" or "Approved"))
//                 throw new UpdateException("InvalidState", "Only Pending or Approved bookings can be modified.");

//             var now = DateTime.UtcNow;
//             var cutoff = b.SlotStartUtc.AddMinutes(-_opts.CancelCutoffMinutes);
//             if (now >= cutoff) throw new UpdateException("CancelCutoff", "Cannot modify within cutoff window.");

//             var st = await _stations.GetByIdAsync(b.StationId, ct)
//                      ?? throw new ValidationException("InvalidStation", "Station not found.");

//             if (req.Minutes != st.DefaultSlotMinutes)
//                 throw new ValidationException("InvalidMinutes", $"Minutes must be {st.DefaultSlotMinutes}.");
//             if (!IsValidTime(req.StartTime))
//                 throw new ValidationException("InvalidTime", "StartTime must be HH:mm.");
//             if (!DateOnly.TryParseExact(req.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
//                 throw new ValidationException("InvalidDate", "LocalDate must be yyyy-MM-dd.");

//             var sch = await _stations.GetScheduleAsync(st.Id!, ct);
//             var localStart = ParseLocal(st.HoursTimezone, d, req.StartTime);
//             var localEnd = localStart.AddMinutes(req.Minutes);
//             if (!IsWithinSchedule(localStart, localEnd, sch))
//                 throw new ValidationException("InvalidScheduleRange", "Requested time is outside station open hours.");

//             var cap = ResolveCapacityForDate(st, sch, localStart.Date);
//             if (cap <= 0) throw new UpdateException("StationClosed", "Station is closed on that date.");

//             var newStartUtc = ToUtc(localStart, st.HoursTimezone);
//             var newEndUtc = ToUtc(localEnd, st.HoursTimezone);

//             // Shortcut: only notes changed
//             if (newStartUtc == b.SlotStartUtc && newEndUtc == b.SlotEndUtc)
//             {
//                 b.Notes = string.IsNullOrWhiteSpace(req.Notes) ? b.Notes : req.Notes.Trim();
//                 b.UpdatedAtUtc = now;
//                 b.UpdatedBy = ownerNic;
//                 await _repo.ReplaceAsync(b, ct);
//                 return ToResponse(b);
//             }

//             // Reserve new slot before releasing old
//             await _repo.EnsureInventoryDocAsync(st.Id!, newStartUtc, newEndUtc, cap, ct);
//             var reservedNew = await _repo.TryReserveAsync(st.Id!, newStartUtc, ct);
//             if (!reservedNew) throw new UpdateException("CapacityFull", "New slot is full.");

//             // Release old capacity
//             await _repo.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);

//             // Apply new times
//             b.SlotStartUtc = newStartUtc;
//             b.SlotEndUtc = newEndUtc;
//             b.SlotStartLocal = $"{d:yyyy-MM-dd}T{req.StartTime}";
//             b.SlotMinutes = req.Minutes;
//             b.Notes = string.IsNullOrWhiteSpace(req.Notes) ? b.Notes : req.Notes.Trim();
//             b.UpdatedAtUtc = now;
//             b.UpdatedBy = ownerNic;

//             // If Approved, reissue QR hash (token not returned here)
//             if (b.Status == "Approved")
//             {
//                 var (_, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);
//                 b.QrTokenHash = hash;
//                 b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             }

//             await _repo.ReplaceAsync(b, ct);
//             return ToResponse(b);
//         }

//         public async Task<BookingResponse> CancelAsync(string id, string ownerNic, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

//             if (!string.Equals(b.OwnerNic, ownerNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");

//             if (b.Status is not ("Pending" or "Approved"))
//                 throw new UpdateException("InvalidState", "Only Pending/Approved can be cancelled.");

//             var now = DateTime.UtcNow;
//             var cutoff = b.SlotStartUtc.AddMinutes(-_opts.CancelCutoffMinutes);
//             if (now >= cutoff) throw new UpdateException("CancelCutoff", "Cannot cancel within cutoff window.");

//             // release capacity
//             await _repo.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);

//             b.Status = "Cancelled";
//             b.CancelledAtUtc = now;
//             b.CancelledBy = ownerNic;
//             b.UpdatedAtUtc = now;
//             b.UpdatedBy = ownerNic;

//             await _repo.ReplaceAsync(b, ct);
//             return ToResponse(b);
//         }

//         public async Task<List<BookingListItem>> AdminListAsync(string? stationId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
//         {
//             var list = await _repo.AdminListAsync(stationId, status, fromUtc, toUtc, ct);
//             return list.Select(b => new BookingListItem
//             {
//                 Id = b.Id!,
//                 BookingCode = b.BookingCode,
//                 StationId = b.StationId,
//                 Status = b.Status,
//                 SlotStartUtc = b.SlotStartUtc,
//                 SlotMinutes = b.SlotMinutes
//             }).ToList();
//         }

//         public async Task<BookingApprovalResponse> ApproveAsync(string id, string staffNic, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
//             if (b.Status != "Pending")
//                 throw new UpdateException("AlreadyFinalized", "Booking is not in Pending state.");
//             if (DateTime.UtcNow >= b.SlotStartUtc)
//                 throw new UpdateException("BookingStartedOrPast", "Too late to approve.");

//             var (token, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);

//             b.Status = "Approved";
//             b.ApprovedAtUtc = DateTime.UtcNow;
//             b.ApprovedBy = staffNic;
//             b.QrTokenHash = hash;
//             b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             b.UpdatedAtUtc = b.ApprovedAtUtc;
//             b.UpdatedBy = staffNic;

//             await _repo.ReplaceAsync(b, ct);

//             return new BookingApprovalResponse
//             {
//                 Id = b.Id!,
//                 Status = b.Status,
//                 QrToken = token, // returned once (client must store/display QR)
//                 QrExpiresAtUtc = b.QrExpiresAtUtc!.Value
//             };
//         }

//         public async Task<BookingResponse> RejectAsync(string id, string staffNic, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
//             if (b.Status != "Pending")
//                 throw new UpdateException("AlreadyFinalized", "Booking is not in Pending state.");
//             if (DateTime.UtcNow >= b.SlotStartUtc)
//                 throw new UpdateException("BookingStartedOrPast", "Too late to reject.");

//             // release capacity
//             await _repo.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);

//             b.Status = "Rejected";
//             b.RejectedAtUtc = DateTime.UtcNow;
//             b.RejectedBy = staffNic;
//             b.UpdatedAtUtc = b.RejectedAtUtc;
//             b.UpdatedBy = staffNic;

//             await _repo.ReplaceAsync(b, ct);
//             return ToResponse(b);
//         }

//         // NEW: Owner/staff can re-issue a QR token for an already Approved booking
//         public async Task<BookingApprovalResponse> IssueQrAsync(string id, string actorNic, bool isStaff, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

//             if (!isStaff && !string.Equals(b.OwnerNic, actorNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");

//             if (!string.Equals(b.Status, "Approved", StringComparison.OrdinalIgnoreCase))
//                 throw new UpdateException("InvalidState", "Only Approved bookings can have QR issued.");

//             var (token, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);
//             b.QrTokenHash = hash;
//             b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             b.UpdatedAtUtc = DateTime.UtcNow;
//             b.UpdatedBy = actorNic;

//             var ok = await _repo.ReplaceAsync(b, ct);
//             if (!ok) throw new UpdateException("ConcurrencyConflict", "Failed to update booking with new QR.");

//             return new BookingApprovalResponse
//             {
//                 Id = b.Id!,
//                 Status = b.Status,
//                 QrToken = token,
//                 QrExpiresAtUtc = b.QrExpiresAtUtc!.Value
//             };
//         }

//         // ----------------- helpers (inside class) -----------------

//         private static BookingResponse ToResponse(Booking b) => new BookingResponse
//         {
//             Id = b.Id!,
//             BookingCode = b.BookingCode,
//             OwnerNic = b.OwnerNic,
//             StationId = b.StationId,
//             Status = b.Status,
//             SlotStartLocal = b.SlotStartLocal,
//             SlotStartUtc = b.SlotStartUtc,
//             SlotEndUtc = b.SlotEndUtc,
//             SlotMinutes = b.SlotMinutes,
//             Notes = b.Notes,
//             CreatedAtUtc = b.CreatedAtUtc,
//             UpdatedAtUtc = b.UpdatedAtUtc,
//             QrExpiresAtUtc = b.QrExpiresAtUtc
//         };

//         // Accept 24h formats like "06:00" or "6:00"
//         private static bool IsValidTime(string t) =>
//             TimeOnly.TryParseExact(t, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

//         // Build a local (unspecified-kind) DateTime from DateOnly + HH:mm
//         private static DateTime ParseLocal(string tz, DateOnly date, string hhmm)
//         {
//             var time = TimeOnly.ParseExact(hhmm, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture);
//             var dtLocal = date.ToDateTime(time); // Unspecified
//             return DateTime.SpecifyKind(dtLocal, DateTimeKind.Unspecified);
//         }

//         private static DateTime ToUtc(DateTime localUnspecified, string tzId)
//         {
//             TimeZoneInfo tz;
//             try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
//             catch { tz = TimeZoneInfo.FindSystemTimeZoneById("UTC"); }
//             return TimeZoneInfo.ConvertTimeToUtc(localUnspecified, tz);
//         }

//         private static bool IsWithinSchedule(DateTime localStart, DateTime localEnd, StationSchedule? sch)
//         {
//             if (sch is null) return false;

//             var dow = localStart.DayOfWeek;
//             var ranges = dow switch
//             {
//                 DayOfWeek.Monday => sch.Weekly.Mon,
//                 DayOfWeek.Tuesday => sch.Weekly.Tue,
//                 DayOfWeek.Wednesday => sch.Weekly.Wed,
//                 DayOfWeek.Thursday => sch.Weekly.Thu,
//                 DayOfWeek.Friday => sch.Weekly.Fri,
//                 DayOfWeek.Saturday => sch.Weekly.Sat,
//                 DayOfWeek.Sunday => sch.Weekly.Sun,
//                 _ => new List<DayTimeRange>()
//             };

//             var dateStr = localStart.ToString("yyyy-MM-dd");
//             if (sch.Exceptions.Any(e => e.Date == dateStr && e.Closed)) return false;

//             // Compare as times-of-day
//             var startTime = TimeSpan.Parse(localStart.ToString("HH\\:mm"));
//             var endTime = TimeSpan.Parse(localEnd.ToString("HH\\:mm"));

//             foreach (var r in ranges)
//             {
//                 if (!TimeSpan.TryParseExact(r.Start, "hh\\:mm", CultureInfo.InvariantCulture, out var s)) continue;
//                 if (!TimeSpan.TryParseExact(r.End, "hh\\:mm", CultureInfo.InvariantCulture, out var e)) continue;
//                 if (s <= startTime && endTime <= e) return true;
//             }
//             return false;
//         }

//         private static string MakeCode()
//         {
//             var r = Random.Shared.Next(0x1000, 0xFFFF);
//             return $"BK-{r:X4}";
//         }

//         private static int ResolveCapacityForDate(Station st, StationSchedule? sch, DateTime localDate)
//         {
//             var cap = st.Connectors;
//             if (sch is null) return 0;
//             var ds = localDate.ToString("yyyy-MM-dd");
//             if (sch.Exceptions.Any(e => e.Date == ds && e.Closed)) return 0;
//             var ov = sch.CapacityOverrides.FirstOrDefault(x => x.Date == ds);
//             if (ov is not null && ov.Connectors > 0) cap = ov.Connectors;
//             return cap;
//         }
//     }
// }






// using System.Globalization;
// using System.Linq;
// using EvCharge.Api.Domain;
// using EvCharge.Api.Domain.DTOs;
// using EvCharge.Api.Infrastructure.Errors;
// using EvCharge.Api.Infrastructure.Qr;
// using EvCharge.Api.Options;
// using EvCharge.Api.Repositories;
// using Microsoft.Extensions.Options;

// namespace EvCharge.Api.Services
// {
//     public interface IBookingService
//     {
//         Task<BookingResponse> CreateAsync(string ownerNic, BookingCreateRequest req, CancellationToken ct);
//         Task<List<BookingListItem>> GetMineAsync(string ownerNic, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
//         Task<BookingResponse> GetByIdAsync(string id, string actorNic, bool isStaff, CancellationToken ct);
//         Task<BookingResponse> UpdateAsync(string id, string ownerNic, BookingUpdateRequest req, CancellationToken ct);
//         Task<BookingResponse> CancelAsync(string id, string ownerNic, CancellationToken ct);

//         Task<List<BookingListItem>> AdminListAsync(string? stationId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
//         Task<BookingApprovalResponse> ApproveAsync(string id, string staffNic, CancellationToken ct);
//         Task<BookingResponse> RejectAsync(string id, string staffNic, CancellationToken ct);

//         // allow Owner (or staff) to re-issue a QR token after approval
//         Task<BookingApprovalResponse> IssueQrAsync(string id, string actorNic, bool isStaff, CancellationToken ct);
//     }

//     public class BookingService : IBookingService
//     {
//         private readonly IBookingRepository _repo;
//         private readonly IStationRepository _stations;
//         private readonly IScheduleService _schedules;
//         private readonly IInventoryService _inv;      //  inventory orchestration
//         private readonly IQrTokenService _qr;
//         private readonly BookingOptions _opts;

//         public BookingService(
//             IBookingRepository repo,
//             IStationRepository stations,
//             IScheduleService schedules,
//             IInventoryService inv,                      
//             IQrTokenService qr,
//             IOptions<BookingOptions> opts)
//         {
//             _repo = repo;
//             _stations = stations;
//             _schedules = schedules;
//             _inv = inv;                                
//             _qr = qr;
//             _opts = opts.Value;
//         }

//         public async Task<BookingResponse> CreateAsync(string ownerNic, BookingCreateRequest req, CancellationToken ct)
//         {
//             var st = await _stations.GetByIdAsync(req.StationId, ct)
//                      ?? throw new ValidationException("InvalidStation", "Station not found.");
//             if (st.Status != "Active")
//                 throw new UpdateException("StationNotActive", "Station is not active.");

//             if (req.Minutes != st.DefaultSlotMinutes)
//                 throw new ValidationException("InvalidMinutes", $"Minutes must be {st.DefaultSlotMinutes}.");
//             if (!IsValidTime(req.StartTime))
//                 throw new ValidationException("InvalidTime", "StartTime must be HH:mm.");
//             if (!DateOnly.TryParseExact(req.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
//                 throw new ValidationException("InvalidDate", "LocalDate must be yyyy-MM-dd.");

//             var sch = await _stations.GetScheduleAsync(st.Id!, ct);
//             var localStart = ParseLocal(st.HoursTimezone, d, req.StartTime);
//             var localEnd = localStart.AddMinutes(req.Minutes);
//             if (!IsWithinSchedule(localStart, localEnd, sch))
//                 throw new ValidationException("InvalidScheduleRange", "Requested time is outside station open hours.");

//             var capacity = ResolveCapacityForDate(st, sch, localStart.Date);
//             if (capacity <= 0)
//                 throw new UpdateException("StationClosed", "Station is closed on that date.");

//             var startUtc = ToUtc(localStart, st.HoursTimezone);
//             var endUtc = ToUtc(localEnd, st.HoursTimezone);

//             await _repo.EnsureIndexesAsync(ct);
//             await _inv.EnsureSlotAsync(st.Id!, startUtc, endUtc, capacity, ct); //  via InventoryService

//             var reserved = await _inv.TryReserveAsync(st.Id!, startUtc, ct);    //  via InventoryService
//             if (!reserved)
//                 throw new UpdateException("CapacityFull", "The selected slot is full.");

//             var now = DateTime.UtcNow;
//             var booking = new Booking
//             {
//                 BookingCode = MakeCode(),
//                 OwnerNic = ownerNic,
//                 StationId = st.Id!,
//                 SlotStartUtc = startUtc,
//                 SlotEndUtc = endUtc,
//                 SlotStartLocal = $"{d:yyyy-MM-dd}T{req.StartTime}",
//                 SlotMinutes = req.Minutes,
//                 Status = "Pending",
//                 Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
//                 CreatedAtUtc = now
//             };

//             // Auto-approve policy (per station or global)
//             var auto = st.AutoApproveEnabled || string.Equals(_opts.ApprovalMode, "Auto", StringComparison.OrdinalIgnoreCase);
//             if (auto)
//             {
//                 var (_, hashTemp) = _qr.Create("temp", st.Id!, startUtc); // temporary until we have ID
//                 booking.Status = "Approved";
//                 booking.ApprovedAtUtc = now;
//                 booking.ApprovedBy = "system-auto";
//                 booking.QrTokenHash = hashTemp;
//                 booking.QrExpiresAtUtc = booking.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             }

//             booking.Id = await _repo.CreateAsync(booking, ct);

//             // If auto, re-issue with real ID (hash changes; acceptable)
//             if (auto)
//             {
//                 var (_, hash) = _qr.Create(booking.Id!, st.Id!, startUtc);
//                 booking.QrTokenHash = hash;
//                 await _repo.ReplaceAsync(booking, ct);
//                 return ToResponse(booking);
//             }

//             return ToResponse(booking);
//         }

//         public async Task<List<BookingListItem>> GetMineAsync(string ownerNic, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
//         {
//             var list = await _repo.GetMineAsync(ownerNic, status, fromUtc, toUtc, ct);
//             return list.Select(b => new BookingListItem
//             {
//                 Id = b.Id!,
//                 BookingCode = b.BookingCode,
//                 StationId = b.StationId,
//                 Status = b.Status,
//                 SlotStartUtc = b.SlotStartUtc,
//                 SlotMinutes = b.SlotMinutes
//             }).ToList();
//         }

//         public async Task<BookingResponse> GetByIdAsync(string id, string actorNic, bool isStaff, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
//             if (!isStaff && !string.Equals(b.OwnerNic, actorNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");
//             return ToResponse(b);
//         }

//         public async Task<BookingResponse> UpdateAsync(string id, string ownerNic, BookingUpdateRequest req, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

//             if (!string.Equals(b.OwnerNic, ownerNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");

//             if (b.Status is not ("Pending" or "Approved"))
//                 throw new UpdateException("InvalidState", "Only Pending or Approved bookings can be modified.");

//             var now = DateTime.UtcNow;
//             var cutoff = b.SlotStartUtc.AddMinutes(-_opts.CancelCutoffMinutes);
//             if (now >= cutoff) throw new UpdateException("CancelCutoff", "Cannot modify within cutoff window.");

//             var st = await _stations.GetByIdAsync(b.StationId, ct)
//                      ?? throw new ValidationException("InvalidStation", "Station not found.");

//             if (req.Minutes != st.DefaultSlotMinutes)
//                 throw new ValidationException("InvalidMinutes", $"Minutes must be {st.DefaultSlotMinutes}.");
//             if (!IsValidTime(req.StartTime))
//                 throw new ValidationException("InvalidTime", "StartTime must be HH:mm.");
//             if (!DateOnly.TryParseExact(req.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
//                 throw new ValidationException("InvalidDate", "LocalDate must be yyyy-MM-dd.");

//             var sch = await _stations.GetScheduleAsync(st.Id!, ct);
//             var localStart = ParseLocal(st.HoursTimezone, d, req.StartTime);
//             var localEnd = localStart.AddMinutes(req.Minutes);
//             if (!IsWithinSchedule(localStart, localEnd, sch))
//                 throw new ValidationException("InvalidScheduleRange", "Requested time is outside station open hours.");

//             var cap = ResolveCapacityForDate(st, sch, localStart.Date);
//             if (cap <= 0) throw new UpdateException("StationClosed", "Station is closed on that date.");

//             var newStartUtc = ToUtc(localStart, st.HoursTimezone);
//             var newEndUtc = ToUtc(localEnd, st.HoursTimezone);

//             // Shortcut: only notes changed
//             if (newStartUtc == b.SlotStartUtc && newEndUtc == b.SlotEndUtc)
//             {
//                 b.Notes = string.IsNullOrWhiteSpace(req.Notes) ? b.Notes : req.Notes.Trim();
//                 b.UpdatedAtUtc = now;
//                 b.UpdatedBy = ownerNic;
//                 await _repo.ReplaceAsync(b, ct);
//                 return ToResponse(b);
//             }

//             // Reserve new slot before releasing old
//             await _inv.EnsureSlotAsync(st.Id!, newStartUtc, newEndUtc, cap, ct);  // via InventoryService
//             var reservedNew = await _inv.TryReserveAsync(st.Id!, newStartUtc, ct); // via InventoryService
//             if (!reservedNew) throw new UpdateException("CapacityFull", "New slot is full.");

//             // Release old capacity
//             await _inv.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);             //via InventoryService

//             // Apply new times
//             b.SlotStartUtc = newStartUtc;
//             b.SlotEndUtc = newEndUtc;
//             b.SlotStartLocal = $"{d:yyyy-MM-dd}T{req.StartTime}";
//             b.SlotMinutes = req.Minutes;
//             b.Notes = string.IsNullOrWhiteSpace(req.Notes) ? b.Notes : req.Notes.Trim();
//             b.UpdatedAtUtc = now;
//             b.UpdatedBy = ownerNic;

//             // If Approved, reissue QR hash (token not returned here)
//             if (b.Status == "Approved")
//             {
//                 var (_, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);
//                 b.QrTokenHash = hash;
//                 b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             }

//             await _repo.ReplaceAsync(b, ct);
//             return ToResponse(b);
//         }

//         public async Task<BookingResponse> CancelAsync(string id, string ownerNic, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

//             if (!string.Equals(b.OwnerNic, ownerNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");

//             if (b.Status is not ("Pending" or "Approved"))
//                 throw new UpdateException("InvalidState", "Only Pending/Approved can be cancelled.");

//             var now = DateTime.UtcNow;
//             var cutoff = b.SlotStartUtc.AddMinutes(-_opts.CancelCutoffMinutes);
//             if (now >= cutoff) throw new UpdateException("CancelCutoff", "Cannot cancel within cutoff window.");

//             // release capacity
//             await _inv.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);             // via InventoryService

//             b.Status = "Cancelled";
//             b.CancelledAtUtc = now;
//             b.CancelledBy = ownerNic;
//             b.UpdatedAtUtc = now;
//             b.UpdatedBy = ownerNic;

//             await _repo.ReplaceAsync(b, ct);
//             return ToResponse(b);
//         }

//         public async Task<List<BookingListItem>> AdminListAsync(string? stationId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
//         {
//             var list = await _repo.AdminListAsync(stationId, status, fromUtc, toUtc, ct);
//             return list.Select(b => new BookingListItem
//             {
//                 Id = b.Id!,
//                 BookingCode = b.BookingCode,
//                 StationId = b.StationId,
//                 Status = b.Status,
//                 SlotStartUtc = b.SlotStartUtc,
//                 SlotMinutes = b.SlotMinutes
//             }).ToList();
//         }

//         public async Task<BookingApprovalResponse> ApproveAsync(string id, string staffNic, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
//             if (b.Status != "Pending")
//                 throw new UpdateException("AlreadyFinalized", "Booking is not in Pending state.");
//             if (DateTime.UtcNow >= b.SlotStartUtc)
//                 throw new UpdateException("BookingStartedOrPast", "Too late to approve.");

//             var (token, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);

//             b.Status = "Approved";
//             b.ApprovedAtUtc = DateTime.UtcNow;
//             b.ApprovedBy = staffNic;
//             b.QrTokenHash = hash;
//             b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             b.UpdatedAtUtc = b.ApprovedAtUtc;
//             b.UpdatedBy = staffNic;

//             await _repo.ReplaceAsync(b, ct);

//             return new BookingApprovalResponse
//             {
//                 Id = b.Id!,
//                 Status = b.Status,
//                 QrToken = token, // returned once (client must store/display QR)
//                 QrExpiresAtUtc = b.QrExpiresAtUtc!.Value
//             };
//         }

//         public async Task<BookingResponse> RejectAsync(string id, string staffNic, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
//             if (b.Status != "Pending")
//                 throw new UpdateException("AlreadyFinalized", "Booking is not in Pending state.");
//             if (DateTime.UtcNow >= b.SlotStartUtc)
//                 throw new UpdateException("BookingStartedOrPast", "Too late to reject.");

//             // release capacity
//             await _inv.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);             //  via InventoryService

//             b.Status = "Rejected";
//             b.RejectedAtUtc = DateTime.UtcNow;
//             b.RejectedBy = staffNic;
//             b.UpdatedAtUtc = b.RejectedAtUtc;
//             b.UpdatedBy = staffNic;

//             await _repo.ReplaceAsync(b, ct);
//             return ToResponse(b);
//         }

//         // Owner/staff can re-issue a QR token for an already Approved booking
//         public async Task<BookingApprovalResponse> IssueQrAsync(string id, string actorNic, bool isStaff, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

//             if (!isStaff && !string.Equals(b.OwnerNic, actorNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");

//             if (!string.Equals(b.Status, "Approved", StringComparison.OrdinalIgnoreCase))
//                 throw new UpdateException("InvalidState", "Only Approved bookings can have QR issued.");

//             var (token, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);
//             b.QrTokenHash = hash;
//             b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             b.UpdatedAtUtc = DateTime.UtcNow;
//             b.UpdatedBy = actorNic;

//             var ok = await _repo.ReplaceAsync(b, ct);
//             if (!ok) throw new UpdateException("ConcurrencyConflict", "Failed to update booking with new QR.");

//             return new BookingApprovalResponse
//             {
//                 Id = b.Id!,
//                 Status = b.Status,
//                 QrToken = token,
//                 QrExpiresAtUtc = b.QrExpiresAtUtc!.Value
//             };
//         }

//         // ----------------- helpers -----------------

//         private static BookingResponse ToResponse(Booking b) => new BookingResponse
//         {
//             Id = b.Id!,
//             BookingCode = b.BookingCode,
//             OwnerNic = b.OwnerNic,
//             StationId = b.StationId,
//             Status = b.Status,
//             SlotStartLocal = b.SlotStartLocal,
//             SlotStartUtc = b.SlotStartUtc,
//             SlotEndUtc = b.SlotEndUtc,
//             SlotMinutes = b.SlotMinutes,
//             Notes = b.Notes,
//             CreatedAtUtc = b.CreatedAtUtc,
//             UpdatedAtUtc = b.UpdatedAtUtc,
//             QrExpiresAtUtc = b.QrExpiresAtUtc
//         };

//         // Accept 24h formats like "06:00" or "6:00"
//         private static bool IsValidTime(string t) =>
//             TimeOnly.TryParseExact(t, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

//         // Build a local (unspecified-kind) DateTime from DateOnly + HH:mm
//         private static DateTime ParseLocal(string tz, DateOnly date, string hhmm)
//         {
//             var time = TimeOnly.ParseExact(hhmm, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture);
//             var dtLocal = date.ToDateTime(time); // Unspecified
//             return DateTime.SpecifyKind(dtLocal, DateTimeKind.Unspecified);
//         }

//         private static DateTime ToUtc(DateTime localUnspecified, string tzId)
//         {
//             TimeZoneInfo tz;
//             try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
//             catch { tz = TimeZoneInfo.FindSystemTimeZoneById("UTC"); }
//             return TimeZoneInfo.ConvertTimeToUtc(localUnspecified, tz);
//         }

//         private static bool IsWithinSchedule(DateTime localStart, DateTime localEnd, StationSchedule? sch)
//         {
//             if (sch is null) return false;

//             var dow = localStart.DayOfWeek;
//             var ranges = dow switch
//             {
//                 DayOfWeek.Monday => sch.Weekly.Mon,
//                 DayOfWeek.Tuesday => sch.Weekly.Tue,
//                 DayOfWeek.Wednesday => sch.Weekly.Wed,
//                 DayOfWeek.Thursday => sch.Weekly.Thu,
//                 DayOfWeek.Friday => sch.Weekly.Fri,
//                 DayOfWeek.Saturday => sch.Weekly.Sat,
//                 DayOfWeek.Sunday => sch.Weekly.Sun,
//                 _ => new List<DayTimeRange>()
//             };

//             var dateStr = localStart.ToString("yyyy-MM-dd");
//             if (sch.Exceptions.Any(e => e.Date == dateStr && e.Closed)) return false;

//             // Compare as times-of-day
//             var startTime = TimeSpan.Parse(localStart.ToString("HH\\:mm"));
//             var endTime = TimeSpan.Parse(localEnd.ToString("HH\\:mm"));

//             foreach (var r in ranges)
//             {
//                 if (!TimeSpan.TryParseExact(r.Start, "hh\\:mm", CultureInfo.InvariantCulture, out var s)) continue;
//                 if (!TimeSpan.TryParseExact(r.End, "hh\\:mm", CultureInfo.InvariantCulture, out var e)) continue;
//                 if (s <= startTime && endTime <= e) return true;
//             }
//             return false;
//         }

//         private static string MakeCode()
//         {
//             var r = Random.Shared.Next(0x1000, 0xFFFF);
//             return $"BK-{r:X4}";
//         }

//         private static int ResolveCapacityForDate(Station st, StationSchedule? sch, DateTime localDate)
//         {
//             var cap = st.Connectors;
//             if (sch is null) return 0;
//             var ds = localDate.ToString("yyyy-MM-dd");
//             if (sch.Exceptions.Any(e => e.Date == ds && e.Closed)) return 0;
//             var ov = sch.CapacityOverrides.FirstOrDefault(x => x.Date == ds);
//             if (ov is not null && ov.Connectors > 0) cap = ov.Connectors;
//             return cap;
//         }
//     }
// }
// using System.Globalization;
// using System.Linq;
// using System.Collections.Generic;
// using EvCharge.Api.Domain;
// using EvCharge.Api.Domain.DTOs;
// using EvCharge.Api.Infrastructure.Errors;
// using EvCharge.Api.Infrastructure.Qr;
// using EvCharge.Api.Options;
// using EvCharge.Api.Repositories;
// using Microsoft.Extensions.Options;

// namespace EvCharge.Api.Services
// {
//     public interface IBookingService
//     {
//         Task<BookingResponse> CreateAsync(string ownerNic, BookingCreateRequest req, CancellationToken ct);
//         Task<List<BookingListItem>> GetMineAsync(string ownerNic, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
//         Task<BookingResponse> GetByIdAsync(string id, string actorNic, bool isStaff, CancellationToken ct);
//         Task<BookingResponse> UpdateAsync(string id, string ownerNic, BookingUpdateRequest req, CancellationToken ct);
//         Task<BookingResponse> CancelAsync(string id, string ownerNic, CancellationToken ct);

//         Task<List<BookingListItem>> AdminListAsync(string? stationId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
//         Task<BookingApprovalResponse> ApproveAsync(string id, string staffNic, CancellationToken ct);
//         Task<BookingResponse> RejectAsync(string id, string staffNic, CancellationToken ct);

//         // allow Owner (or staff) to re-issue a QR token after approval
//         Task<BookingApprovalResponse> IssueQrAsync(string id, string actorNic, bool isStaff, CancellationToken ct);
//     }

//     public class BookingService : IBookingService
//     {
//         private readonly IBookingRepository _repo;
//         private readonly IStationRepository _stations;
//         private readonly IScheduleService _schedules;
//         private readonly IInventoryService _inv;      // inventory orchestration
//         private readonly IQrTokenService _qr;
//         private readonly IPolicyService _policy;
//         private readonly BookingOptions _opts;

//         // ===== NEW: emit points =====
//         private readonly IAuditService _audit;
//         private readonly INotificationService _notify;

//         public BookingService(
//             IBookingRepository repo,
//             IStationRepository stations,
//             IScheduleService schedules,
//             IInventoryService inv,
//             IQrTokenService qr,
//             IPolicyService policy, 
//             IOptions<BookingOptions> opts,
//             IAuditService audit,
//             INotificationService notify)
//         {
//             _repo = repo;
//             _stations = stations;
//             _schedules = schedules;
//             _inv = inv;
//             _qr = qr;
//             _policy = policy; 
//             _opts = opts.Value;
//             _audit = audit;
//             _notify = notify;
//         }

//         public async Task<BookingResponse> CreateAsync(string ownerNic, BookingCreateRequest req, CancellationToken ct)
//         {
//             var st = await _stations.GetByIdAsync(req.StationId, ct)
//                      ?? throw new ValidationException("InvalidStation", "Station not found.");
//             if (st.Status != "Active")
//                 throw new UpdateException("StationNotActive", "Station is not active.");

//             if (req.Minutes != st.DefaultSlotMinutes)
//                 throw new ValidationException("InvalidMinutes", $"Minutes must be {st.DefaultSlotMinutes}.");
//             if (!IsValidTime(req.StartTime))
//                 throw new ValidationException("InvalidTime", "StartTime must be HH:mm.");
//             if (!DateOnly.TryParseExact(req.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
//                 throw new ValidationException("InvalidDate", "LocalDate must be yyyy-MM-dd.");

//             var sch = await _stations.GetScheduleAsync(st.Id!, ct);
//             var localStart = ParseLocal(st.HoursTimezone, d, req.StartTime);
//             var localEnd = localStart.AddMinutes(req.Minutes);
//             if (!IsWithinSchedule(localStart, localEnd, sch))
//                 throw new ValidationException("InvalidScheduleRange", "Requested time is outside station open hours.");

//             var capacity = ResolveCapacityForDate(st, sch, localStart.Date);
//             if (capacity <= 0)
//                 throw new UpdateException("StationClosed", "Station is closed on that date.");

//             var startUtc = ToUtc(localStart, st.HoursTimezone);
//             var endUtc = ToUtc(localEnd, st.HoursTimezone);

//             await _repo.EnsureIndexesAsync(ct);
//             await _inv.EnsureSlotAsync(st.Id!, startUtc, endUtc, capacity, ct); // via InventoryService

//             var reserved = await _inv.TryReserveAsync(st.Id!, startUtc, ct);    // via InventoryService
//             if (!reserved)
//                 throw new UpdateException("CapacityFull", "The selected slot is full.");

//             var now = DateTime.UtcNow;
//             var booking = new Booking
//             {
//                 BookingCode = MakeCode(),
//                 OwnerNic = ownerNic,
//                 StationId = st.Id!,
//                 SlotStartUtc = startUtc,
//                 SlotEndUtc = endUtc,
//                 SlotStartLocal = $"{d:yyyy-MM-dd}T{req.StartTime}",
//                 SlotMinutes = req.Minutes,
//                 Status = "Pending",
//                 Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
//                 CreatedAtUtc = now
//             };

//             // Auto-approve policy (per station or global)
//             var auto = st.AutoApproveEnabled || string.Equals(_opts.ApprovalMode, "Auto", StringComparison.OrdinalIgnoreCase);
//             if (auto)
//             {
//                 var (_, hashTemp) = _qr.Create("temp", st.Id!, startUtc); // temporary until we have ID
//                 booking.Status = "Approved";
//                 booking.ApprovedAtUtc = now;
//                 booking.ApprovedBy = "system-auto";
//                 booking.QrTokenHash = hashTemp;
//                 booking.QrExpiresAtUtc = booking.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             }

//             booking.Id = await _repo.CreateAsync(booking, ct);

//             // If auto, re-issue with real ID (hash changes; acceptable)
//             if (auto)
//             {
//                 var (_, hash) = _qr.Create(booking.Id!, st.Id!, startUtc);
//                 booking.QrTokenHash = hash;
//                 await _repo.ReplaceAsync(booking, ct);

//                 // ===== AUDIT + NOTIFY (auto-approve) =====
//                 await _audit.LogAsync("booking", booking.Id!, "Approved", "system-auto", "System",
//                     new Dictionary<string, object?>
//                     {
//                         ["qrExpiresAtUtc"] = booking.QrExpiresAtUtc,
//                         ["stationId"] = booking.StationId
//                     }, ct);

//                 await _notify.EnqueueAsync("BookingApproved", booking.OwnerNic,
//                     "Your booking was approved",
//                     $"Booking {booking.BookingCode} is approved. Use the QR in your app to check in.",
//                     new Dictionary<string, object?>
//                     {
//                         ["bookingId"] = booking.Id,
//                         ["bookingCode"] = booking.BookingCode
//                     }, ct);

//                 return ToResponse(booking);
//             }

//             return ToResponse(booking);
//         }

//         public async Task<List<BookingListItem>> GetMineAsync(string ownerNic, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
//         {
//             var list = await _repo.GetMineAsync(ownerNic, status, fromUtc, toUtc, ct);
//             return list.Select(b => new BookingListItem
//             {
//                 Id = b.Id!,
//                 BookingCode = b.BookingCode,
//                 StationId = b.StationId,
//                 Status = b.Status,
//                 SlotStartUtc = b.SlotStartUtc,
//                 SlotMinutes = b.SlotMinutes
//             }).ToList();
//         }

//         public async Task<BookingResponse> GetByIdAsync(string id, string actorNic, bool isStaff, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
//             if (!isStaff && !string.Equals(b.OwnerNic, actorNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");
//             return ToResponse(b);
//         }

//         public async Task<BookingResponse> UpdateAsync(string id, string ownerNic, BookingUpdateRequest req, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

//             if (!string.Equals(b.OwnerNic, ownerNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");

//             if (b.Status is not ("Pending" or "Approved"))
//                 throw new UpdateException("InvalidState", "Only Pending or Approved bookings can be modified.");

//             var now = DateTime.UtcNow;
//             var cutoff = b.SlotStartUtc.AddMinutes(-_opts.CancelCutoffMinutes);
//             if (now >= cutoff) throw new UpdateException("CancelCutoff", "Cannot modify within cutoff window.");

//             var st = await _stations.GetByIdAsync(b.StationId, ct)
//                      ?? throw new ValidationException("InvalidStation", "Station not found.");

//             if (req.Minutes != st.DefaultSlotMinutes)
//                 throw new ValidationException("InvalidMinutes", $"Minutes must be {st.DefaultSlotMinutes}.");
//             if (!IsValidTime(req.StartTime))
//                 throw new ValidationException("InvalidTime", "StartTime must be HH:mm.");
//             if (!DateOnly.TryParseExact(req.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
//                 throw new ValidationException("InvalidDate", "LocalDate must be yyyy-MM-dd.");

//             var sch = await _stations.GetScheduleAsync(st.Id!, ct);
//             var localStart = ParseLocal(st.HoursTimezone, d, req.StartTime);
//             var localEnd = localStart.AddMinutes(req.Minutes);
//             if (!IsWithinSchedule(localStart, localEnd, sch))
//                 throw new ValidationException("InvalidScheduleRange", "Requested time is outside station open hours.");

//             var cap = ResolveCapacityForDate(st, sch, localStart.Date);
//             if (cap <= 0) throw new UpdateException("StationClosed", "Station is closed on that date.");

//             var newStartUtc = ToUtc(localStart, st.HoursTimezone);
//             var newEndUtc = ToUtc(localEnd, st.HoursTimezone);

//             // Shortcut: only notes changed
//             if (newStartUtc == b.SlotStartUtc && newEndUtc == b.SlotEndUtc)
//             {
//                 b.Notes = string.IsNullOrWhiteSpace(req.Notes) ? b.Notes : req.Notes.Trim();
//                 b.UpdatedAtUtc = now;
//                 b.UpdatedBy = ownerNic;
//                 await _repo.ReplaceAsync(b, ct);
//                 return ToResponse(b);
//             }

//             // Reserve new slot before releasing old
//             await _inv.EnsureSlotAsync(st.Id!, newStartUtc, newEndUtc, cap, ct);   // via InventoryService
//             var reservedNew = await _inv.TryReserveAsync(st.Id!, newStartUtc, ct); // via InventoryService
//             if (!reservedNew) throw new UpdateException("CapacityFull", "New slot is full.");

//             // Release old capacity
//             await _inv.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);              // via InventoryService

//             // Apply new times
//             b.SlotStartUtc = newStartUtc;
//             b.SlotEndUtc = newEndUtc;
//             b.SlotStartLocal = $"{d:yyyy-MM-dd}T{req.StartTime}";
//             b.SlotMinutes = req.Minutes;
//             b.Notes = string.IsNullOrWhiteSpace(req.Notes) ? b.Notes : req.Notes.Trim();
//             b.UpdatedAtUtc = now;
//             b.UpdatedBy = ownerNic;

//             // If Approved, reissue QR hash (token not returned here)
//             if (b.Status == "Approved")
//             {
//                 var (_, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);
//                 b.QrTokenHash = hash;
//                 b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             }

//             await _repo.ReplaceAsync(b, ct);
//             return ToResponse(b);
//         }

//         public async Task<BookingResponse> CancelAsync(string id, string ownerNic, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

//             if (!string.Equals(b.OwnerNic, ownerNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");

//             if (b.Status is not ("Pending" or "Approved"))
//                 throw new UpdateException("InvalidState", "Only Pending/Approved can be cancelled.");

//             var now = DateTime.UtcNow;
//             var cutoff = b.SlotStartUtc.AddMinutes(-_opts.CancelCutoffMinutes);
//             if (now >= cutoff) throw new UpdateException("CancelCutoff", "Cannot cancel within cutoff window.");

//             // release capacity
//             await _inv.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);              // via InventoryService

//             b.Status = "Cancelled";
//             b.CancelledAtUtc = now;
//             b.CancelledBy = ownerNic;
//             b.UpdatedAtUtc = now;
//             b.UpdatedBy = ownerNic;

//             await _repo.ReplaceAsync(b, ct);

//             // ===== AUDIT + NOTIFY =====
//             await _audit.LogAsync("booking", b.Id!, "Cancelled", ownerNic, "Owner",
//                 new Dictionary<string, object?>
//                 {
//                     ["stationId"] = b.StationId,
//                     ["slotStartUtc"] = b.SlotStartUtc,
//                     ["slotMinutes"] = b.SlotMinutes
//                 }, ct);

//             await _notify.EnqueueAsync("BookingCancelled", b.OwnerNic,
//                 "Your booking was cancelled",
//                 $"Booking {b.BookingCode} has been cancelled.",
//                 new Dictionary<string, object?>
//                 {
//                     ["bookingId"] = b.Id,
//                     ["bookingCode"] = b.BookingCode
//                 }, ct);

//             return ToResponse(b);
//         }

//         public async Task<List<BookingListItem>> AdminListAsync(string? stationId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
//         {
//             var list = await _repo.AdminListAsync(stationId, status, fromUtc, toUtc, ct);
//             return list.Select(b => new BookingListItem
//             {
//                 Id = b.Id!,
//                 BookingCode = b.BookingCode,
//                 StationId = b.StationId,
//                 Status = b.Status,
//                 SlotStartUtc = b.SlotStartUtc,
//                 SlotMinutes = b.SlotMinutes
//             }).ToList();
//         }

//         public async Task<BookingApprovalResponse> ApproveAsync(string id, string staffNic, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
//             if (b.Status != "Pending")
//                 throw new UpdateException("AlreadyFinalized", "Booking is not in Pending state.");
//             if (DateTime.UtcNow >= b.SlotStartUtc)
//                 throw new UpdateException("BookingStartedOrPast", "Too late to approve.");

//             var (token, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);

//             b.Status = "Approved";
//             b.ApprovedAtUtc = DateTime.UtcNow;
//             b.ApprovedBy = staffNic;
//             b.QrTokenHash = hash;
//             b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             b.UpdatedAtUtc = b.ApprovedAtUtc;
//             b.UpdatedBy = staffNic;

//             await _repo.ReplaceAsync(b, ct);

//             // ===== AUDIT + NOTIFY =====
//             await _audit.LogAsync("booking", b.Id!, "Approved", staffNic, "Staff",
//                 new Dictionary<string, object?>
//                 {
//                     ["qrExpiresAtUtc"] = b.QrExpiresAtUtc,
//                     ["stationId"] = b.StationId
//                 }, ct);

//             await _notify.EnqueueAsync("BookingApproved", b.OwnerNic,
//                 "Your booking was approved",
//                 $"Booking {b.BookingCode} is approved. Use the QR in your app to check in.",
//                 new Dictionary<string, object?>
//                 {
//                     ["bookingId"] = b.Id,
//                     ["bookingCode"] = b.BookingCode
//                 }, ct);

//             return new BookingApprovalResponse
//             {
//                 Id = b.Id!,
//                 Status = b.Status,
//                 QrToken = token, // returned once (client must store/display QR)
//                 QrExpiresAtUtc = b.QrExpiresAtUtc!.Value
//             };
//         }

//         public async Task<BookingResponse> RejectAsync(string id, string staffNic, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
//             if (b.Status != "Pending")
//                 throw new UpdateException("AlreadyFinalized", "Booking is not in Pending state.");
//             if (DateTime.UtcNow >= b.SlotStartUtc)
//                 throw new UpdateException("BookingStartedOrPast", "Too late to reject.");

//             // release capacity
//             await _inv.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);              // via InventoryService

//             b.Status = "Rejected";
//             b.RejectedAtUtc = DateTime.UtcNow;
//             b.RejectedBy = staffNic;
//             b.UpdatedAtUtc = b.RejectedAtUtc;
//             b.UpdatedBy = staffNic;

//             await _repo.ReplaceAsync(b, ct);

//             // ===== AUDIT + NOTIFY =====
//             await _audit.LogAsync("booking", b.Id!, "Rejected", staffNic, "Staff",
//                 new Dictionary<string, object?>
//                 {
//                     ["stationId"] = b.StationId,
//                     ["slotStartUtc"] = b.SlotStartUtc
//                 }, ct);

//             await _notify.EnqueueAsync("BookingRejected", b.OwnerNic,
//                 "Your booking was rejected",
//                 $"Booking {b.BookingCode} was rejected.",
//                 new Dictionary<string, object?>
//                 {
//                     ["bookingId"] = b.Id,
//                     ["bookingCode"] = b.BookingCode
//                 }, ct);

//             return ToResponse(b);
//         }

//         // Owner/staff can re-issue a QR token for an already Approved booking
//         public async Task<BookingApprovalResponse> IssueQrAsync(string id, string actorNic, bool isStaff, CancellationToken ct)
//         {
//             var b = await _repo.GetByIdAsync(id, ct)
//                     ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

//             if (!isStaff && !string.Equals(b.OwnerNic, actorNic, StringComparison.Ordinal))
//                 throw new UpdateException("Forbidden", "Not your booking.");

//             if (!string.Equals(b.Status, "Approved", StringComparison.OrdinalIgnoreCase))
//                 throw new UpdateException("InvalidState", "Only Approved bookings can have QR issued.");

//             var (token, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);
//             b.QrTokenHash = hash;
//             b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
//             b.UpdatedAtUtc = DateTime.UtcNow;
//             b.UpdatedBy = actorNic;

//             var ok = await _repo.ReplaceAsync(b, ct);
//             if (!ok) throw new UpdateException("ConcurrencyConflict", "Failed to update booking with new QR.");

//             // ===== AUDIT =====
//             await _audit.LogAsync("booking", b.Id!, "QrIssued", actorNic, isStaff ? "Staff" : "Owner",
//                 new Dictionary<string, object?>
//                 {
//                     ["qrExpiresAtUtc"] = b.QrExpiresAtUtc
//                 }, ct);

//             return new BookingApprovalResponse
//             {
//                 Id = b.Id!,
//                 Status = b.Status,
//                 QrToken = token,
//                 QrExpiresAtUtc = b.QrExpiresAtUtc!.Value
//             };
//         }

//         // ----------------- helpers -----------------

//         private static BookingResponse ToResponse(Booking b) => new BookingResponse
//         {
//             Id = b.Id!,
//             BookingCode = b.BookingCode,
//             OwnerNic = b.OwnerNic,
//             StationId = b.StationId,
//             Status = b.Status,
//             SlotStartLocal = b.SlotStartLocal,
//             SlotStartUtc = b.SlotStartUtc,
//             SlotEndUtc = b.SlotEndUtc,
//             SlotMinutes = b.SlotMinutes,
//             Notes = b.Notes,
//             CreatedAtUtc = b.CreatedAtUtc,
//             UpdatedAtUtc = b.UpdatedAtUtc,
//             QrExpiresAtUtc = b.QrExpiresAtUtc
//         };

//         // Accept 24h formats like "06:00" or "6:00"
//         private static bool IsValidTime(string t) =>
//             TimeOnly.TryParseExact(t, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

//         // Build a local (unspecified-kind) DateTime from DateOnly + HH:mm
//         private static DateTime ParseLocal(string tz, DateOnly date, string hhmm)
//         {
//             var time = TimeOnly.ParseExact(hhmm, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture);
//             var dtLocal = date.ToDateTime(time); // Unspecified
//             return DateTime.SpecifyKind(dtLocal, DateTimeKind.Unspecified);
//         }

//         private static DateTime ToUtc(DateTime localUnspecified, string tzId)
//         {
//             TimeZoneInfo tz;
//             try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
//             catch { tz = TimeZoneInfo.FindSystemTimeZoneById("UTC"); }
//             return TimeZoneInfo.ConvertTimeToUtc(localUnspecified, tz);
//         }

//         private static bool IsWithinSchedule(DateTime localStart, DateTime localEnd, StationSchedule? sch)
//         {
//             if (sch is null) return false;

//             var dow = localStart.DayOfWeek;
//             var ranges = dow switch
//             {
//                 DayOfWeek.Monday => sch.Weekly.Mon,
//                 DayOfWeek.Tuesday => sch.Weekly.Tue,
//                 DayOfWeek.Wednesday => sch.Weekly.Wed,
//                 DayOfWeek.Thursday => sch.Weekly.Thu,
//                 DayOfWeek.Friday => sch.Weekly.Fri,
//                 DayOfWeek.Saturday => sch.Weekly.Sat,
//                 DayOfWeek.Sunday => sch.Weekly.Sun,
//                 _ => new List<DayTimeRange>()
//             };

//             var dateStr = localStart.ToString("yyyy-MM-dd");
//             if (sch.Exceptions.Any(e => e.Date == dateStr && e.Closed)) return false;

//             // Compare as times-of-day
//             var startTime = TimeSpan.Parse(localStart.ToString("HH\\:mm"));
//             var endTime = TimeSpan.Parse(localEnd.ToString("HH\\:mm"));

//             foreach (var r in ranges)
//             {
//                 if (!TimeSpan.TryParseExact(r.Start, "hh\\:mm", CultureInfo.InvariantCulture, out var s)) continue;
//                 if (!TimeSpan.TryParseExact(r.End, "hh\\:mm", CultureInfo.InvariantCulture, out var e)) continue;
//                 if (s <= startTime && endTime <= e) return true;
//             }
//             return false;
//         }

//         private static string MakeCode()
//         {
//             var r = Random.Shared.Next(0x1000, 0xFFFF);
//             return $"BK-{r:X4}";
//         }

//         private static int ResolveCapacityForDate(Station st, StationSchedule? sch, DateTime localDate)
//         {
//             var cap = st.Connectors;
//             if (sch is null) return 0;
//             var ds = localDate.ToString("yyyy-MM-dd");
//             if (sch.Exceptions.Any(e => e.Date == ds && e.Closed)) return 0;
//             var ov = sch.CapacityOverrides.FirstOrDefault(x => x.Date == ds);
//             if (ov is not null && ov.Connectors > 0) cap = ov.Connectors;
//             return cap;
//         }
//     }
// }


using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using EvCharge.Api.Domain;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Infrastructure.Errors;
using EvCharge.Api.Infrastructure.Qr;
using EvCharge.Api.Options;
using EvCharge.Api.Repositories;
using Microsoft.Extensions.Options;

namespace EvCharge.Api.Services
{
    public interface IBookingService
    {
        Task<BookingResponse> CreateAsync(string ownerNic, BookingCreateRequest req, CancellationToken ct);
        Task<List<BookingListItem>> GetMineAsync(string ownerNic, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
        Task<BookingResponse> GetByIdAsync(string id, string actorNic, bool isStaff, CancellationToken ct);
        Task<BookingResponse> UpdateAsync(string id, string ownerNic, BookingUpdateRequest req, CancellationToken ct);
        Task<BookingResponse> CancelAsync(string id, string ownerNic, CancellationToken ct);

        Task<List<BookingListItem>> AdminListAsync(string? stationId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
        Task<BookingApprovalResponse> ApproveAsync(string id, string staffNic, CancellationToken ct);
        Task<BookingResponse> RejectAsync(string id, string staffNic, CancellationToken ct);

        // allow Owner (or staff) to re-issue a QR token after approval
        Task<BookingApprovalResponse> IssueQrAsync(string id, string actorNic, bool isStaff, CancellationToken ct);
    }

    public class BookingService : IBookingService
    {
        private readonly IBookingRepository _repo;
        private readonly IStationRepository _stations;
        private readonly IScheduleService _schedules;
        private readonly IInventoryService _inv;      // inventory orchestration
        private readonly IQrTokenService _qr;
        private readonly IPolicyService _policy;
        private readonly BookingOptions _opts;

        // ===== emit points =====
        private readonly IAuditService _audit;
        private readonly INotificationService _notify;

        public BookingService(
            IBookingRepository repo,
            IStationRepository stations,
            IScheduleService schedules,
            IInventoryService inv,
            IQrTokenService qr,
            IPolicyService policy,
            IOptions<BookingOptions> opts,
            IAuditService audit,
            INotificationService notify)
        {
            _repo = repo;
            _stations = stations;
            _schedules = schedules;
            _inv = inv;
            _qr = qr;
            _policy = policy;
            _opts = opts.Value;
            _audit = audit;
            _notify = notify;
        }

        public async Task<BookingResponse> CreateAsync(string ownerNic, BookingCreateRequest req, CancellationToken ct)
        {
            var st = await _stations.GetByIdAsync(req.StationId, ct)
                     ?? throw new ValidationException("InvalidStation", "Station not found.");
            if (st.Status != "Active")
                throw new UpdateException("StationNotActive", "Station is not active.");

            if (req.Minutes != st.DefaultSlotMinutes)
                throw new ValidationException("InvalidMinutes", $"Minutes must be {st.DefaultSlotMinutes}.");
            if (!IsValidTime(req.StartTime))
                throw new ValidationException("InvalidTime", "StartTime must be HH:mm.");
            if (!DateOnly.TryParseExact(req.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                throw new ValidationException("InvalidDate", "LocalDate must be yyyy-MM-dd.");

            var sch = await _stations.GetScheduleAsync(st.Id!, ct);
            var localStart = ParseLocal(st.HoursTimezone, d, req.StartTime);
            var localEnd = localStart.AddMinutes(req.Minutes);
            if (!IsWithinSchedule(localStart, localEnd, sch))
                throw new ValidationException("InvalidScheduleRange", "Requested time is outside station open hours.");

            // ---- Policy: booking horizon (≤N days ahead, no past) ----
            _policy.EnsureWithinBookingHorizon(st, localStart, DateTime.UtcNow);

            var capacity = ResolveCapacityForDate(st, sch, localStart.Date);
            if (capacity <= 0)
                throw new UpdateException("StationClosed", "Station is closed on that date.");

            var startUtc = ToUtc(localStart, st.HoursTimezone);
            var endUtc = ToUtc(localEnd, st.HoursTimezone);

            await _repo.EnsureIndexesAsync(ct);
            await _inv.EnsureSlotAsync(st.Id!, startUtc, endUtc, capacity, ct); // via InventoryService

            var reserved = await _inv.TryReserveAsync(st.Id!, startUtc, ct);    // via InventoryService
            if (!reserved)
                throw new UpdateException("CapacityFull", "The selected slot is full.");

            var now = DateTime.UtcNow;
            var booking = new Booking
            {
                BookingCode = MakeCode(),
                OwnerNic = ownerNic,
                StationId = st.Id!,
                SlotStartUtc = startUtc,
                SlotEndUtc = endUtc,
                SlotStartLocal = $"{d:yyyy-MM-dd}T{req.StartTime}",
                SlotMinutes = req.Minutes,
                Status = "Pending",
                Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
                CreatedAtUtc = now
            };

            // Auto-approve policy (per station or global)
            var auto = st.AutoApproveEnabled || string.Equals(_opts.ApprovalMode, "Auto", StringComparison.OrdinalIgnoreCase);
            if (auto)
            {
                var (_, hashTemp) = _qr.Create("temp", st.Id!, startUtc); // temporary until we have ID
                booking.Status = "Approved";
                booking.ApprovedAtUtc = now;
                booking.ApprovedBy = "system-auto";
                booking.QrTokenHash = hashTemp;
                booking.QrExpiresAtUtc = booking.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
            }

            booking.Id = await _repo.CreateAsync(booking, ct);

            // If auto, re-issue with real ID (hash changes; acceptable)
            if (auto)
            {
                var (_, hash) = _qr.Create(booking.Id!, st.Id!, startUtc);
                booking.QrTokenHash = hash;
                await _repo.ReplaceAsync(booking, ct);

                // ===== AUDIT + NOTIFY (auto-approve) =====
                await _audit.LogAsync("booking", booking.Id!, "Approved", "system-auto", "System",
                    new Dictionary<string, object?>
                    {
                        ["qrExpiresAtUtc"] = booking.QrExpiresAtUtc,
                        ["stationId"] = booking.StationId
                    }, ct);

                await _notify.EnqueueAsync("BookingApproved", booking.OwnerNic,
                    "Your booking was approved",
                    $"Booking {booking.BookingCode} is approved. Use the QR in your app to check in.",
                    new Dictionary<string, object?>
                    {
                        ["bookingId"] = booking.Id,
                        ["bookingCode"] = booking.BookingCode
                    }, ct);

                return ToResponse(booking);
            }

            return ToResponse(booking);
        }

        public async Task<List<BookingListItem>> GetMineAsync(string ownerNic, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            var list = await _repo.GetMineAsync(ownerNic, status, fromUtc, toUtc, ct);
            return list.Select(b => new BookingListItem
            {
                Id = b.Id!,
                BookingCode = b.BookingCode,
                StationId = b.StationId,
                Status = b.Status,
                SlotStartUtc = b.SlotStartUtc,
                SlotMinutes = b.SlotMinutes
            }).ToList();
        }

        public async Task<BookingResponse> GetByIdAsync(string id, string actorNic, bool isStaff, CancellationToken ct)
        {
            var b = await _repo.GetByIdAsync(id, ct)
                    ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
            if (!isStaff && !string.Equals(b.OwnerNic, actorNic, StringComparison.Ordinal))
                throw new UpdateException("Forbidden", "Not your booking.");
            return ToResponse(b);
        }

        public async Task<BookingResponse> UpdateAsync(string id, string ownerNic, BookingUpdateRequest req, CancellationToken ct)
        {
            var b = await _repo.GetByIdAsync(id, ct)
                    ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

            if (!string.Equals(b.OwnerNic, ownerNic, StringComparison.Ordinal))
                throw new UpdateException("Forbidden", "Not your booking.");

            if (b.Status is not ("Pending" or "Approved"))
                throw new UpdateException("InvalidState", "Only Pending or Approved bookings can be modified.");

            var now = DateTime.UtcNow;

            // ---- Policy: owner modify lock (e.g., ≤12h) ----
            _policy.EnsureOwnerModifyAllowed(b, now);

            var st = await _stations.GetByIdAsync(b.StationId, ct)
                     ?? throw new ValidationException("InvalidStation", "Station not found.");

            if (req.Minutes != st.DefaultSlotMinutes)
                throw new ValidationException("InvalidMinutes", $"Minutes must be {st.DefaultSlotMinutes}.");
            if (!IsValidTime(req.StartTime))
                throw new ValidationException("InvalidTime", "StartTime must be HH:mm.");
            if (!DateOnly.TryParseExact(req.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                throw new ValidationException("InvalidDate", "LocalDate must be yyyy-MM-dd.");

            var sch = await _stations.GetScheduleAsync(st.Id!, ct);
            var localStart = ParseLocal(st.HoursTimezone, d, req.StartTime);
            var localEnd = localStart.AddMinutes(req.Minutes);
            if (!IsWithinSchedule(localStart, localEnd, sch))
                throw new ValidationException("InvalidScheduleRange", "Requested time is outside station open hours.");

            // ---- Policy: horizon also applies on reschedule ----
            _policy.EnsureWithinBookingHorizon(st, localStart, now);

            var cap = ResolveCapacityForDate(st, sch, localStart.Date);
            if (cap <= 0) throw new UpdateException("StationClosed", "Station is closed on that date.");

            var newStartUtc = ToUtc(localStart, st.HoursTimezone);
            var newEndUtc = ToUtc(localEnd, st.HoursTimezone);

            // Shortcut: only notes changed
            if (newStartUtc == b.SlotStartUtc && newEndUtc == b.SlotEndUtc)
            {
                b.Notes = string.IsNullOrWhiteSpace(req.Notes) ? b.Notes : req.Notes.Trim();
                b.UpdatedAtUtc = now;
                b.UpdatedBy = ownerNic;
                await _repo.ReplaceAsync(b, ct);
                return ToResponse(b);
            }

            // Reserve new slot before releasing old
            await _inv.EnsureSlotAsync(st.Id!, newStartUtc, newEndUtc, cap, ct);   // via InventoryService
            var reservedNew = await _inv.TryReserveAsync(st.Id!, newStartUtc, ct); // via InventoryService
            if (!reservedNew) throw new UpdateException("CapacityFull", "New slot is full.");

            // Release old capacity
            await _inv.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);              // via InventoryService

            // Apply new times
            b.SlotStartUtc = newStartUtc;
            b.SlotEndUtc = newEndUtc;
            b.SlotStartLocal = $"{d:yyyy-MM-dd}T{req.StartTime}";
            b.SlotMinutes = req.Minutes;
            b.Notes = string.IsNullOrWhiteSpace(req.Notes) ? b.Notes : req.Notes.Trim();
            b.UpdatedAtUtc = now;
            b.UpdatedBy = ownerNic;

            // If Approved, reissue QR hash (token not returned here)
            if (b.Status == "Approved")
            {
                var (_, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);
                b.QrTokenHash = hash;
                b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
            }

            await _repo.ReplaceAsync(b, ct);
            return ToResponse(b);
        }

        public async Task<BookingResponse> CancelAsync(string id, string ownerNic, CancellationToken ct)
        {
            var b = await _repo.GetByIdAsync(id, ct)
                    ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

            if (!string.Equals(b.OwnerNic, ownerNic, StringComparison.Ordinal))
                throw new UpdateException("Forbidden", "Not your booking.");

            if (b.Status is not ("Pending" or "Approved"))
                throw new UpdateException("InvalidState", "Only Pending/Approved can be cancelled.");

            var now = DateTime.UtcNow;

            // ---- Policy: owner cancel lock (e.g., ≤12h) ----
            _policy.EnsureOwnerCancelAllowed(b, now);

            // release capacity
            await _inv.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);              // via InventoryService

            b.Status = "Cancelled";
            b.CancelledAtUtc = now;
            b.CancelledBy = ownerNic;
            b.UpdatedAtUtc = now;
            b.UpdatedBy = ownerNic;

            await _repo.ReplaceAsync(b, ct);

            // ===== AUDIT + NOTIFY =====
            await _audit.LogAsync("booking", b.Id!, "Cancelled", ownerNic, "Owner",
                new Dictionary<string, object?>
                {
                    ["stationId"] = b.StationId,
                    ["slotStartUtc"] = b.SlotStartUtc,
                    ["slotMinutes"] = b.SlotMinutes
                }, ct);

            await _notify.EnqueueAsync("BookingCancelled", b.OwnerNic,
                "Your booking was cancelled",
                $"Booking {b.BookingCode} has been cancelled.",
                new Dictionary<string, object?>
                {
                    ["bookingId"] = b.Id,
                    ["bookingCode"] = b.BookingCode
                }, ct);

            return ToResponse(b);
        }

        public async Task<List<BookingListItem>> AdminListAsync(string? stationId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            var list = await _repo.AdminListAsync(stationId, status, fromUtc, toUtc, ct);
            return list.Select(b => new BookingListItem
            {
                Id = b.Id!,
                BookingCode = b.BookingCode,
                StationId = b.StationId,
                Status = b.Status,
                SlotStartUtc = b.SlotStartUtc,
                SlotMinutes = b.SlotMinutes
            }).ToList();
        }

        public async Task<BookingApprovalResponse> ApproveAsync(string id, string staffNic, CancellationToken ct)
        {
            var b = await _repo.GetByIdAsync(id, ct)
                    ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
            if (b.Status != "Pending")
                throw new UpdateException("AlreadyFinalized", "Booking is not in Pending state.");
            if (DateTime.UtcNow >= b.SlotStartUtc)
                throw new UpdateException("BookingStartedOrPast", "Too late to approve.");

            var (token, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);

            b.Status = "Approved";
            b.ApprovedAtUtc = DateTime.UtcNow;
            b.ApprovedBy = staffNic;
            b.QrTokenHash = hash;
            b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
            b.UpdatedAtUtc = b.ApprovedAtUtc;
            b.UpdatedBy = staffNic;

            await _repo.ReplaceAsync(b, ct);

            // ===== AUDIT + NOTIFY =====
            await _audit.LogAsync("booking", b.Id!, "Approved", staffNic, "Staff",
                new Dictionary<string, object?>
                {
                    ["qrExpiresAtUtc"] = b.QrExpiresAtUtc,
                    ["stationId"] = b.StationId
                }, ct);

            await _notify.EnqueueAsync("BookingApproved", b.OwnerNic,
                "Your booking was approved",
                $"Booking {b.BookingCode} is approved. Use the QR in your app to check in.",
                new Dictionary<string, object?>
                {
                    ["bookingId"] = b.Id,
                    ["bookingCode"] = b.BookingCode
                }, ct);

            return new BookingApprovalResponse
            {
                Id = b.Id!,
                Status = b.Status,
                QrToken = token, // returned once (client must store/display QR)
                QrExpiresAtUtc = b.QrExpiresAtUtc!.Value
            };
        }

        public async Task<BookingResponse> RejectAsync(string id, string staffNic, CancellationToken ct)
        {
            var b = await _repo.GetByIdAsync(id, ct)
                    ?? throw new NotFoundException("BookingNotFound", "Booking not found.");
            if (b.Status != "Pending")
                throw new UpdateException("AlreadyFinalized", "Booking is not in Pending state.");
            if (DateTime.UtcNow >= b.SlotStartUtc)
                throw new UpdateException("BookingStartedOrPast", "Too late to reject.");

            // release capacity
            await _inv.ReleaseAsync(b.StationId, b.SlotStartUtc, ct);              // via InventoryService

            b.Status = "Rejected";
            b.RejectedAtUtc = DateTime.UtcNow;
            b.RejectedBy = staffNic;
            b.UpdatedAtUtc = b.RejectedAtUtc;
            b.UpdatedBy = staffNic;

            await _repo.ReplaceAsync(b, ct);

            // ===== AUDIT + NOTIFY =====
            await _audit.LogAsync("booking", b.Id!, "Rejected", staffNic, "Staff",
                new Dictionary<string, object?>
                {
                    ["stationId"] = b.StationId,
                    ["slotStartUtc"] = b.SlotStartUtc
                }, ct);

            await _notify.EnqueueAsync("BookingRejected", b.OwnerNic,
                "Your booking was rejected",
                $"Booking {b.BookingCode} was rejected.",
                new Dictionary<string, object?>
                {
                    ["bookingId"] = b.Id,
                    ["bookingCode"] = b.BookingCode
                }, ct);

            return ToResponse(b);
        }

        // Owner/staff can re-issue a QR token for an already Approved booking
        public async Task<BookingApprovalResponse> IssueQrAsync(string id, string actorNic, bool isStaff, CancellationToken ct)
        {
            var b = await _repo.GetByIdAsync(id, ct)
                    ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

            if (!isStaff && !string.Equals(b.OwnerNic, actorNic, StringComparison.Ordinal))
                throw new UpdateException("Forbidden", "Not your booking.");

            if (!string.Equals(b.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                throw new UpdateException("InvalidState", "Only Approved bookings can have QR issued.");

            var (token, hash) = _qr.Create(b.Id!, b.StationId, b.SlotStartUtc);
            b.QrTokenHash = hash;
            b.QrExpiresAtUtc = b.SlotStartUtc.AddMinutes(_opts.QrExpiryAfterStartMinutes);
            b.UpdatedAtUtc = DateTime.UtcNow;
            b.UpdatedBy = actorNic;

            var ok = await _repo.ReplaceAsync(b, ct);
            if (!ok) throw new UpdateException("ConcurrencyConflict", "Failed to update booking with new QR.");

            // ===== AUDIT =====
            await _audit.LogAsync("booking", b.Id!, "QrIssued", actorNic, isStaff ? "Staff" : "Owner",
                new Dictionary<string, object?>
                {
                    ["qrExpiresAtUtc"] = b.QrExpiresAtUtc
                }, ct);

            return new BookingApprovalResponse
            {
                Id = b.Id!,
                Status = b.Status,
                QrToken = token,
                QrExpiresAtUtc = b.QrExpiresAtUtc!.Value
            };
        }

        // ----------------- helpers -----------------

        private static BookingResponse ToResponse(Booking b) => new BookingResponse
        {
            Id = b.Id!,
            BookingCode = b.BookingCode,
            OwnerNic = b.OwnerNic,
            StationId = b.StationId,
            Status = b.Status,
            SlotStartLocal = b.SlotStartLocal,
            SlotStartUtc = b.SlotStartUtc,
            SlotEndUtc = b.SlotEndUtc,
            SlotMinutes = b.SlotMinutes,
            Notes = b.Notes,
            CreatedAtUtc = b.CreatedAtUtc,
            UpdatedAtUtc = b.UpdatedAtUtc,
            QrExpiresAtUtc = b.QrExpiresAtUtc
        };

        // Accept 24h formats like "06:00" or "6:00"
        private static bool IsValidTime(string t) =>
            TimeOnly.TryParseExact(t, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

        // Build a local (unspecified-kind) DateTime from DateOnly + HH:mm
        private static DateTime ParseLocal(string tz, DateOnly date, string hhmm)
        {
            var time = TimeOnly.ParseExact(hhmm, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture);
            var dtLocal = date.ToDateTime(time); // Unspecified
            return DateTime.SpecifyKind(dtLocal, DateTimeKind.Unspecified);
        }

        private static DateTime ToUtc(DateTime localUnspecified, string tzId)
        {
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("UTC"); }
            return TimeZoneInfo.ConvertTimeToUtc(localUnspecified, tz);
        }

        private static bool IsWithinSchedule(DateTime localStart, DateTime localEnd, StationSchedule? sch)
        {
            if (sch is null) return false;

            var dow = localStart.DayOfWeek;
            var ranges = dow switch
            {
                DayOfWeek.Monday => sch.Weekly.Mon,
                DayOfWeek.Tuesday => sch.Weekly.Tue,
                DayOfWeek.Wednesday => sch.Weekly.Wed,
                DayOfWeek.Thursday => sch.Weekly.Thu,
                DayOfWeek.Friday => sch.Weekly.Fri,
                DayOfWeek.Saturday => sch.Weekly.Sat,
                DayOfWeek.Sunday => sch.Weekly.Sun,
                _ => new List<DayTimeRange>()
            };

            var dateStr = localStart.ToString("yyyy-MM-dd");
            if (sch.Exceptions.Any(e => e.Date == dateStr && e.Closed)) return false;

            // Compare as times-of-day
            var startTime = TimeSpan.Parse(localStart.ToString("HH\\:mm"));
            var endTime = TimeSpan.Parse(localEnd.ToString("HH\\:mm"));

            foreach (var r in ranges)
            {
                if (!TimeSpan.TryParseExact(r.Start, "hh\\:mm", CultureInfo.InvariantCulture, out var s)) continue;
                if (!TimeSpan.TryParseExact(r.End, "hh\\:mm", CultureInfo.InvariantCulture, out var e)) continue;
                if (s <= startTime && endTime <= e) return true;
            }
            return false;
        }

        private static string MakeCode()
        {
            var r = Random.Shared.Next(0x1000, 0xFFFF);
            return $"BK-{r:X4}";
        }

        private static int ResolveCapacityForDate(Station st, StationSchedule? sch, DateTime localDate)
        {
            var cap = st.Connectors;
            if (sch is null) return 0;
            var ds = localDate.ToString("yyyy-MM-dd");
            if (sch.Exceptions.Any(e => e.Date == ds && e.Closed)) return 0;
            var ov = sch.CapacityOverrides.FirstOrDefault(x => x.Date == ds);
            if (ov is not null && ov.Connectors > 0) cap = ov.Connectors;
            return cap;
        }
    }
}
