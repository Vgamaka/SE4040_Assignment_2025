using System.Linq;
using EvCharge.Api.Domain;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Infrastructure.Errors;
using EvCharge.Api.Infrastructure.Mapping;
using EvCharge.Api.Infrastructure.Validation;
using EvCharge.Api.Repositories;
using MongoDB.Bson;

namespace EvCharge.Api.Services
{
    public interface IStationService
    {
        Task<StationResponse> CreateAsync(StationCreateRequest req, string actorNic, bool isBackOffice, CancellationToken ct);
        Task<StationResponse> GetByIdAsync(string id, CancellationToken ct);
        Task<StationResponse> UpdateAsync(string id, StationUpdateRequest req, string actor, CancellationToken ct);
        Task<StationResponse> ActivateAsync(string id, string actor, CancellationToken ct);
        Task<StationResponse> DeactivateAsync(string id, string actor, CancellationToken ct);
        Task<(List<StationListItem> items, long total)> ListAsync(string? type, string? status, int? minConnectors, int page, int pageSize, CancellationToken ct);
        Task<List<StationListItem>> NearbyAsync(double lat, double lng, double radiusKm, string? type, CancellationToken ct);
        Task<StationScheduleResponse> GetScheduleAsync(string id, CancellationToken ct);
        Task<StationScheduleResponse> UpsertScheduleAsync(string id, StationScheduleUpsertRequest req, string actor, CancellationToken ct);

        Task<(List<StationListItem> items, long total)> ListByBackOfficeAsync(string backOfficeNic, int page, int pageSize, CancellationToken ct);
    }

    public class StationService : IStationService
    {
        private readonly IStationRepository _repo;
        private readonly IScheduleService _schedule;
        private readonly IEvOwnerRepository _owners;
        private readonly IPolicyService _policy;

        public StationService(IStationRepository repo, IScheduleService schedule, IEvOwnerRepository owners, IPolicyService policy)
        {
            _repo = repo;
            _schedule = schedule;
            _owners = owners;
            _policy = policy;
        }

        public async Task<StationResponse> CreateAsync(StationCreateRequest req, string actorNic, bool isBackOffice, CancellationToken ct)
        {
            ValidateCreate(req);

            if (isBackOffice)
            {
                var bo = await _owners.GetByNicAsync(actorNic, ct) ?? throw new NotFoundException("BackOfficeNotFound", "BackOffice not found.");
                if (!bo.Roles.Contains("BackOffice"))
                    throw new UpdateException("Forbidden", "Only BackOffice can create stations.");
                if (bo.BackOfficeProfile?.ApplicationStatus != "Approved")
                    throw new ValidationException("BackOfficeNotApproved", "BackOffice application is not approved.");
            }

            var entity = req.ToEntity(actorNic);
            entity.CreatedAtUtc = DateTime.UtcNow;

            if (isBackOffice)
                entity.BackOfficeNic = actorNic;

            entity.Id = await _repo.CreateAsync(entity, ct);
            return entity.ToResponse();
        }

        public async Task<StationResponse> GetByIdAsync(string id, CancellationToken ct)
        {
            var e = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("StationNotFound", "Station not found.");
            return e.ToResponse();
        }

        public async Task<StationResponse> UpdateAsync(string id, StationUpdateRequest req, string actor, CancellationToken ct)
        {
            var e = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("StationNotFound", "Station not found.");
            ValidateUpdate(req);
            e.ApplyUpdate(req, actor);
            var ok = await _repo.ReplaceAsync(e, ct);
            if (!ok) throw new UpdateException("ConcurrencyConflict", "Failed to update station. Please retry.");
            return e.ToResponse();
        }

        public async Task<StationResponse> ActivateAsync(string id, string actor, CancellationToken ct)
        {
            var e = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("StationNotFound", "Station not found.");
            e.Status = "Active"; e.UpdatedAtUtc = DateTime.UtcNow; e.UpdatedBy = actor;
            await _repo.ReplaceAsync(e, ct);
            return e.ToResponse();
        }

        public async Task<StationResponse> DeactivateAsync(string id, string actor, CancellationToken ct)
        {
            var e = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("StationNotFound", "Station not found.");

            // --- NEW: policy deactivation guard ---
            await _policy.EnsureStationCanDeactivateAsync(e.Id!, ct);

            e.Status = "Inactive"; e.UpdatedAtUtc = DateTime.UtcNow; e.UpdatedBy = actor;
            await _repo.ReplaceAsync(e, ct);
            return e.ToResponse();
        }

        public async Task<(List<StationListItem> items, long total)> ListAsync(string? type, string? status, int? minConnectors, int page, int pageSize, CancellationToken ct)
        {
            var (items, total) = await _repo.ListAsync(type, status, minConnectors, page, Math.Clamp(pageSize, 1, 100), ct);
            var result = new List<StationListItem>(items.Count);
            foreach (var s in items)
            {
                var sch = await _repo.GetScheduleAsync(s.Id!, ct);
                var summary = _schedule.ComputeSevenDaySlotSummary(s, sch)
                    .Select(x => new AvailabilitySummaryItem { Date = x.date.ToString("yyyy-MM-dd"), AvailableSlots = x.slots }).ToList();
                result.Add(s.ToListItem(summary));
            }
            return (result, total);
        }

        public async Task<List<StationListItem>> NearbyAsync(double lat, double lng, double radiusKm, string? type, CancellationToken ct)
        {
            var items = await _repo.NearbyAsync(lat, lng, radiusKm, type, ct);
            var result = new List<StationListItem>(items.Count);
            foreach (var s in items)
            {
                var sch = await _repo.GetScheduleAsync(s.Id!, ct);
                var summary = _schedule.ComputeSevenDaySlotSummary(s, sch)
                    .Select(x => new AvailabilitySummaryItem { Date = x.date.ToString("yyyy-MM-dd"), AvailableSlots = x.slots }).ToList();
                result.Add(s.ToListItem(summary));
            }
            return result;
        }

        public async Task<StationScheduleResponse> GetScheduleAsync(string id, CancellationToken ct)
        {
            var e = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("StationNotFound", "Station not found.");
            var schedule = await _repo.GetScheduleAsync(e.Id!, ct) ?? new StationSchedule { StationId = e.Id!, Weekly = new(), Exceptions = new(), CapacityOverrides = new(), UpdatedAtUtc = DateTime.UtcNow };
            return schedule.ToResponse();
        }


public async Task<StationScheduleResponse> UpsertScheduleAsync(string id, StationScheduleUpsertRequest req, string actor, CancellationToken ct)
{
    var e = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("StationNotFound", "Station not found.");
    ValidateScheduleUpsert(req);

    // Check if a schedule already exists
    var existing = await _repo.GetScheduleAsync(e.Id!, ct);

    var schedule = new StationSchedule
    {
        StationId = e.Id!,
        Weekly = new WeeklySchedule
        {
            Mon = req.Weekly.Mon.Select(x => new DayTimeRange { Start = x.Start, End = x.End }).ToList(),
            Tue = req.Weekly.Tue.Select(x => new DayTimeRange { Start = x.Start, End = x.End }).ToList(),
            Wed = req.Weekly.Wed.Select(x => new DayTimeRange { Start = x.Start, End = x.End }).ToList(),
            Thu = req.Weekly.Thu.Select(x => new DayTimeRange { Start = x.Start, End = x.End }).ToList(),
            Fri = req.Weekly.Fri.Select(x => new DayTimeRange { Start = x.Start, End = x.End }).ToList(),
            Sat = req.Weekly.Sat.Select(x => new DayTimeRange { Start = x.Start, End = x.End }).ToList(),
            Sun = req.Weekly.Sun.Select(x => new DayTimeRange { Start = x.Start, End = x.End }).ToList(),
        },
        Exceptions = req.Exceptions.Select(x => new ScheduleException { Date = x.Date, Closed = x.Closed }).ToList(),
        CapacityOverrides = req.CapacityOverrides.Select(x => new CapacityOverride { Date = x.Date, Connectors = x.Connectors }).ToList(),
        UpdatedAtUtc = DateTime.UtcNow
    };

    // Reuse Id if present, otherwise generate a new one
    schedule.Id = existing?.Id ?? ObjectId.GenerateNewId().ToString();

    await _repo.UpsertScheduleAsync(schedule, ct);
    return schedule.ToResponse();
}

        public async Task<(List<StationListItem> items, long total)> ListByBackOfficeAsync(string backOfficeNic, int page, int pageSize, CancellationToken ct)
        {
            var (items, total) = await _repo.ListByBackOfficeAsync(backOfficeNic, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
            var result = new List<StationListItem>(items.Count);
            foreach (var s in items)
            {
                var sch = await _repo.GetScheduleAsync(s.Id!, ct);
                var summary = _schedule.ComputeSevenDaySlotSummary(s, sch)
                    .Select(x => new AvailabilitySummaryItem { Date = x.date.ToString("yyyy-MM-dd"), AvailableSlots = x.slots }).ToList();
                result.Add(s.ToListItem(summary));
            }
            return (result, total);
        }

        // -------- validations --------
        private static void ValidateCreate(StationCreateRequest r)
        {
            if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length is < 2 or > 120)
                throw new ValidationException("InvalidName", "Name must be between 2 and 120 characters.");
            if (!ScheduleValidator.ValidType(r.Type))
                throw new ValidationException("InvalidType", "Type must be AC or DC.");
            if (r.Connectors < 1)
                throw new ValidationException("InvalidConnectors", "Connectors must be >= 1.");
            if (!GeoValidator.IsValidLat(r.Lat) || !GeoValidator.IsValidLng(r.Lng))
                throw new ValidationException("InvalidLocation", "Lat/Lng is invalid.");
            if (!ScheduleValidator.ValidSlotMinutes(r.DefaultSlotMinutes))
                throw new ValidationException("InvalidSlotMinutes", "Slot minutes must be one of 30,45,60,90,120.");
            if (!ScheduleValidator.ValidPricingModel(r.Pricing?.Model))
                throw new ValidationException("InvalidPricingModel", "Pricing model must be flat|hourly|kwh.");
            if (r.Pricing is { Base: < 0 or > 1_000_000, PerHour: < 0 or > 1_000_000, PerKwh: < 0 or > 1_000_000, TaxPct: < 0 or > 100 })
                throw new ValidationException("InvalidPricing", "Pricing values are out of range.");
        }

        private static void ValidateUpdate(StationUpdateRequest r)
        {
            if (r.Name is not null && (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length is < 2 or > 120))
                throw new ValidationException("InvalidName", "Name must be between 2 and 120 characters.");
            if (r.Type is not null && !ScheduleValidator.ValidType(r.Type))
                throw new ValidationException("InvalidType", "Type must be AC or DC.");
            if (r.Connectors is not null && r.Connectors < 1)
                throw new ValidationException("InvalidConnectors", "Connectors must be >= 1.");
            if ((r.Lat is not null && !GeoValidator.IsValidLat(r.Lat.Value)) || (r.Lng is not null && !GeoValidator.IsValidLng(r.Lng.Value)))
                throw new ValidationException("InvalidLocation", "Lat/Lng is invalid.");
            if (r.DefaultSlotMinutes is not null && !ScheduleValidator.ValidSlotMinutes(r.DefaultSlotMinutes.Value))
                throw new ValidationException("InvalidSlotMinutes", "Slot minutes must be one of 30,45,60,90,120.");
            if (r.Pricing is not null)
            {
                if (!ScheduleValidator.ValidPricingModel(r.Pricing.Model))
                    throw new ValidationException("InvalidPricingModel", "Pricing model must be flat|hourly|kwh.");
                if (r.Pricing is { Base: < 0 or > 1_000_000, PerHour: < 0 or > 1_000_000, PerKwh: < 0 or > 1_000_000, TaxPct: < 0 or > 100 })
                    throw new ValidationException("InvalidPricing", "Pricing values are out of range.");
            }
        }

        private static void ValidateScheduleUpsert(StationScheduleUpsertRequest r)
        {
            foreach (var day in new[] { r.Weekly.Mon, r.Weekly.Tue, r.Weekly.Wed, r.Weekly.Thu, r.Weekly.Fri, r.Weekly.Sat, r.Weekly.Sun })
            {
                var ordered = day.Select(x =>
                {
                    if (!ScheduleValidator.IsValidRange(x.Start, x.End))
                        throw new ValidationException("InvalidTimeRange", $"Invalid time range {x.Start}-{x.End}");
                    return (Start: TimeSpan.Parse(x.Start), End: TimeSpan.Parse(x.End));
                }).OrderBy(x => x.Start).ToList();

                for (int i = 1; i < ordered.Count; i++)
                    if (ordered[i].Start < ordered[i - 1].End)
                        throw new ValidationException("OverlappingRanges", "Overlapping time ranges in a day.");
            }
        }
    }
}
