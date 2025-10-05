using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using EvCharge.Api.Domain;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Domain.Entities;
using EvCharge.Api.Infrastructure.Errors;
using EvCharge.Api.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EvCharge.Api.Services
{
    public interface ISessionService
    {
        Task<QrVerifyResponse> VerifyQrAsync(QrVerifyRequest req, CancellationToken ct);
        Task<BookingResponse> CheckInAsync(SessionCheckInRequest req, string actorNic, ClaimsPrincipal principal, CancellationToken ct);
        Task<SessionReceiptResponse> FinalizeAsync(SessionFinalizeRequest req, string actorNic, ClaimsPrincipal principal, CancellationToken ct);
    }

    public class SessionService : ISessionService
    {
        private readonly ILogger<SessionService> _logger;
        private readonly BookingOptions _opts;

        private readonly IMongoCollection<Booking> _bookings;
        private readonly IMongoCollection<Session> _sessions;

        // ===== NEW: policy + emit points =====
        private readonly IPolicyService _policy;
        private readonly IAuditService _audit;
        private readonly INotificationService _notify;

        public SessionService(
            ILogger<SessionService> logger,
            IOptions<BookingOptions> bookingOpts,
            IMongoDatabase db,
            IPolicyService policy,
            IAuditService audit,
            INotificationService notify)
        {
            _logger = logger;
            _opts = bookingOpts.Value;

            _bookings = db.GetCollection<Booking>("bookings");
            _sessions = db.GetCollection<Session>("sessions");

            _policy = policy;
            _audit = audit;
            _notify = notify;

            EnsureIndexesAsync().GetAwaiter().GetResult();
        }

        private async Task EnsureIndexesAsync()
        {
            var keys = Builders<Booking>.IndexKeys.Ascending(x => x.QrTokenHash);
            await _bookings.Indexes.CreateOneAsync(new CreateIndexModel<Booking>(keys, new CreateIndexOptions { Name = "ix_qr_hash" }));

            var sKeys = Builders<Session>.IndexKeys.Ascending(x => x.BookingId);
            await _sessions.Indexes.CreateOneAsync(new CreateIndexModel<Session>(sKeys, new CreateIndexOptions { Name = "ix_session_booking" }));
        }

        private static string HashToken(string token)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }

        public async Task<QrVerifyResponse> VerifyQrAsync(QrVerifyRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.QrToken))
                throw new ValidationException("MissingQrToken", "QR token is required.");

            var nowUtc = DateTime.UtcNow;
            var hash = HashToken(req.QrToken);

            var booking = await _bookings.Find(x => x.QrTokenHash == hash).FirstOrDefaultAsync(ct);
            if (booking is null)
                return new QrVerifyResponse { Valid = false, Message = "Invalid QR." };

            var exp = booking.QrExpiresAtUtc;
            var status = booking.Status ?? "Pending";

            if (!exp.HasValue || exp.Value <= nowUtc)
                return new QrVerifyResponse { Valid = false, BookingId = booking.Id, StationId = booking.StationId, ExpUtc = exp, Status = status, Message = "QR expired." };

            if (!string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
                return new QrVerifyResponse { Valid = false, BookingId = booking.Id, StationId = booking.StationId, ExpUtc = exp, Status = status, Message = "Booking not in Approved state." };

            return new QrVerifyResponse
            {
                Valid = true,
                BookingId = booking.Id,
                StationId = booking.StationId,
                ExpUtc = exp,
                Status = status
            };
        }

        public async Task<BookingResponse> CheckInAsync(SessionCheckInRequest req, string actorNic, ClaimsPrincipal principal, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.QrToken))
                throw new ValidationException("MissingQrToken", "QR token is required.");

            var verify = await VerifyQrAsync(new QrVerifyRequest { QrToken = req.QrToken }, ct);
            if (!verify.Valid)
                throw new AuthException("InvalidQr", verify.Message ?? "QR invalid.");

            if (!string.IsNullOrWhiteSpace(req.BookingId) && !string.Equals(req.BookingId, verify.BookingId, StringComparison.Ordinal))
                throw new ValidationException("QrBookingMismatch", "QR does not belong to provided booking.");

            // Optional operator station scope enforcement
            var stationClaims = principal?.Claims.Where(c => c.Type == "OperatorStationId").Select(c => c.Value).ToHashSet() ?? new();
            if (stationClaims.Count > 0 && verify.StationId is not null && !stationClaims.Contains(verify.StationId))
                throw new AuthException("ForbiddenStation", "You are not assigned to this station.");

            var booking = await _bookings.Find(x => x.Id == verify.BookingId).FirstOrDefaultAsync(ct)
                          ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

            var nowUtc = DateTime.UtcNow;
            
            // ---- Policy: enforce earliest + latest (slot end + grace) check-in window
            _policy.EnsureCheckInWindow(booking, nowUtc);

            var upd = Builders<Booking>.Update
                .Set(x => x.Status, "CheckedIn")
                .Set(x => x.UpdatedAtUtc, nowUtc)
                .Set(x => x.UpdatedBy, actorNic);

            var res = await _bookings.UpdateOneAsync(
                Builders<Booking>.Filter.And(
                    Builders<Booking>.Filter.Eq(x => x.Id, booking.Id),
                    Builders<Booking>.Filter.Eq(x => x.Status, "Approved")
                ),
                upd,
                cancellationToken: ct
            );
            if (res.ModifiedCount == 0)
                throw new UpdateException("StateConflict", "Booking state changed, cannot check-in.");

            var session = new Session
            {
                Id = Guid.NewGuid().ToString("N"),
                BookingId = booking.Id!,
                StationId = booking.StationId,
                OwnerNIC = booking.OwnerNic,
                CreatedAtUtc = nowUtc,
                CheckInUtc = nowUtc,
                Status = "CheckedIn"
            };
            await _sessions.InsertOneAsync(session, cancellationToken: ct);

            // ===== AUDIT + NOTIFY =====
            var actorRole = ResolvePrimaryRole(principal);
            await _audit.LogAsync("booking", booking.Id!, "CheckedIn", actorNic, actorRole,
                new Dictionary<string, object?>
                {
                    ["sessionId"] = session.Id,
                    ["stationId"] = booking.StationId
                }, ct);

            await _notify.EnqueueAsync("CheckIn", booking.OwnerNic,
                "You have checked in",
                $"Checked in for booking {booking.BookingCode}.",
                new Dictionary<string, object?>
                {
                    ["bookingId"] = booking.Id,
                    ["sessionId"] = session.Id
                }, ct);

            return new BookingResponse
            {
                Id = booking.Id!,
                BookingCode = booking.BookingCode,
                OwnerNic = booking.OwnerNic,
                StationId = booking.StationId,
                Status = "CheckedIn",
                SlotStartLocal = booking.SlotStartLocal,
                SlotStartUtc = booking.SlotStartUtc,
                SlotEndUtc = booking.SlotEndUtc,
                SlotMinutes = booking.SlotMinutes,
                Notes = booking.Notes,
                CreatedAtUtc = booking.CreatedAtUtc,
                UpdatedAtUtc = nowUtc,
                QrExpiresAtUtc = booking.QrExpiresAtUtc
            };
        }

        public async Task<SessionReceiptResponse> FinalizeAsync(SessionFinalizeRequest req, string actorNic, ClaimsPrincipal principal, CancellationToken ct)
        {
            if (req.EnergyKwh < 0 || req.UnitPrice < 0)
                throw new ValidationException("InvalidAmounts", "Energy and price must be non-negative.");

            var nowUtc = DateTime.UtcNow;

            var booking = await _bookings.Find(x => x.Id == req.BookingId).FirstOrDefaultAsync(ct)
                          ?? throw new NotFoundException("BookingNotFound", "Booking not found.");

            if (!string.Equals(booking.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase))
                throw new UpdateException("NotCheckedIn", "Booking is not in CheckedIn state.");

            var session = await _sessions.Find(x => x.BookingId == req.BookingId).FirstOrDefaultAsync(ct)
                          ?? throw new NotFoundException("SessionNotFound", "Session not found for booking.");

            if (session.CompletedAtUtc.HasValue)
                throw new UpdateException("AlreadyFinalized", "Session already finalized.");

            var total = Math.Round(req.EnergyKwh * req.UnitPrice, 2, MidpointRounding.AwayFromZero);

            var updBooking = Builders<Booking>.Update
                .Set(x => x.Status, "Completed")
                .Set(x => x.UpdatedAtUtc, nowUtc)
                .Set(x => x.UpdatedBy, actorNic);

            var updRes = await _bookings.UpdateOneAsync(
                Builders<Booking>.Filter.And(
                    Builders<Booking>.Filter.Eq(x => x.Id, booking.Id),
                    Builders<Booking>.Filter.Eq(x => x.Status, "CheckedIn")
                ),
                updBooking,
                cancellationToken: ct
            );
            if (updRes.ModifiedCount == 0)
                throw new UpdateException("StateConflict", "Booking state changed, cannot finalize.");

            var updSession = Builders<Session>.Update
                .Set(s => s.CompletedAtUtc, nowUtc)
                .Set(s => s.EnergyKwh, req.EnergyKwh)
                .Set(s => s.UnitPrice, req.UnitPrice)
                .Set(s => s.Total, total)
                .Set(s => s.Notes, req.Notes)
                .Set(s => s.Status, "Completed");

            await _sessions.UpdateOneAsync(s => s.Id == session.Id, updSession, cancellationToken: ct);

            // ===== AUDIT + NOTIFY =====
            var actorRole = ResolvePrimaryRole(principal);
            await _audit.LogAsync("booking", booking.Id!, "Completed", actorNic, actorRole,
                new Dictionary<string, object?>
                {
                    ["sessionId"] = session.Id,
                    ["energyKwh"] = req.EnergyKwh,
                    ["unitPrice"] = req.UnitPrice,
                    ["total"] = total
                }, ct);

            await _notify.EnqueueAsync("Completed", booking.OwnerNic,
                "Charging session completed",
                $"Booking {booking.BookingCode} is completed. Total: {total:0.00}.",
                new Dictionary<string, object?>
                {
                    ["bookingId"] = booking.Id,
                    ["sessionId"] = session.Id,
                    ["total"] = total
                }, ct);

            return new SessionReceiptResponse
            {
                BookingId = booking.Id!,
                BookingCode = booking.BookingCode,
                StationId = booking.StationId,
                SlotStartUtc = booking.SlotStartUtc,
                SlotMinutes = booking.SlotMinutes,
                EnergyKwh = req.EnergyKwh,
                UnitPrice = req.UnitPrice,
                Total = total,
                CompletedAtUtc = nowUtc
            };
        }

        private static string? ResolvePrimaryRole(ClaimsPrincipal? principal)
        {
            if (principal is null) return null;
            var roles = principal.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type.EndsWith("/claims/role"))
                .Select(c => c.Value)
                .ToList();
            return roles.FirstOrDefault();
        }
    }
}
