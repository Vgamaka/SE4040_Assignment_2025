using EvCharge.Api.Domain;
using EvCharge.Api.Domain.DTOs;

namespace EvCharge.Api.Infrastructure.Mapping
{
    public static class StationMapping
    {
        public static Station ToEntity(this StationCreateRequest r, string actor)
        {
            return new Station
            {
                Name = r.Name.Trim(),
                Type = r.Type.Trim(),
                Connectors = r.Connectors,
                Status = "Active",
                AutoApproveEnabled = r.AutoApproveEnabled,
                Location = new GeoPoint { Type = "Point", Coordinates = new[] { r.Lng, r.Lat } },
                DefaultSlotMinutes = r.DefaultSlotMinutes,
                Pricing = new Pricing
                {
                    Model = r.Pricing?.Model ?? "flat",
                    Base = r.Pricing?.Base ?? 0,
                    PerHour = r.Pricing?.PerHour ?? 0,
                    PerKwh = r.Pricing?.PerKwh ?? 0,
                    TaxPct = r.Pricing?.TaxPct ?? 0
                },
                HoursTimezone = string.IsNullOrWhiteSpace(r.HoursTimezone) ? "Asia/Colombo" : r.HoursTimezone.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = null,
                UpdatedBy = actor
            };
        }

        public static void ApplyUpdate(this Station s, StationUpdateRequest r, string actor)
        {
            if (!string.IsNullOrWhiteSpace(r.Name)) s.Name = r.Name.Trim();
            if (!string.IsNullOrWhiteSpace(r.Type)) s.Type = r.Type.Trim();
            if (r.Connectors.HasValue) s.Connectors = r.Connectors.Value;
            if (r.AutoApproveEnabled.HasValue) s.AutoApproveEnabled = r.AutoApproveEnabled.Value; // NEW
            if (r.Lat.HasValue && r.Lng.HasValue) s.Location = new GeoPoint { Type = "Point", Coordinates = new[] { r.Lng.Value, r.Lat.Value } };
            if (r.DefaultSlotMinutes.HasValue) s.DefaultSlotMinutes = r.DefaultSlotMinutes.Value;
            if (!string.IsNullOrWhiteSpace(r.HoursTimezone)) s.HoursTimezone = r.HoursTimezone.Trim();
            if (r.Pricing is not null)
            {
                s.Pricing ??= new Pricing();
                s.Pricing.Model = r.Pricing.Model;
                s.Pricing.Base = r.Pricing.Base;
                s.Pricing.PerHour = r.Pricing.PerHour;
                s.Pricing.PerKwh = r.Pricing.PerKwh;
                s.Pricing.TaxPct = r.Pricing.TaxPct;
            }
            s.UpdatedAtUtc = DateTime.UtcNow;
            s.UpdatedBy = actor;
        }

        public static StationResponse ToResponse(this Station s)
        {
            return new StationResponse
            {
                Id = s.Id!,
                Name = s.Name,
                Type = s.Type,
                BackOfficeNic = s.BackOfficeNic,
                Connectors = s.Connectors,
                Status = s.Status,
                AutoApproveEnabled = s.AutoApproveEnabled,
                Lat = s.Location.Coordinates.Length == 2 ? s.Location.Coordinates[1] : 0,
                Lng = s.Location.Coordinates.Length == 2 ? s.Location.Coordinates[0] : 0,
                DefaultSlotMinutes = s.DefaultSlotMinutes,
                HoursTimezone = s.HoursTimezone,
                Pricing = new PricingDto
                {
                    Model = s.Pricing.Model,
                    Base = s.Pricing.Base,
                    PerHour = s.Pricing.PerHour,
                    PerKwh = s.Pricing.PerKwh,
                    TaxPct = s.Pricing.TaxPct
                },
                CreatedAtUtc = s.CreatedAtUtc,
                UpdatedAtUtc = s.UpdatedAtUtc
            };
        }

        public static StationListItem ToListItem(this Station s, List<AvailabilitySummaryItem> summary)
        {
            return new StationListItem
            {
                Id = s.Id!,
                Name = s.Name,
                Type = s.Type,
                BackOfficeNic = s.BackOfficeNic,
                Connectors = s.Connectors,
                Status = s.Status,
                AutoApproveEnabled = s.AutoApproveEnabled,
                Lat = s.Location.Coordinates.Length == 2 ? s.Location.Coordinates[1] : 0,
                Lng = s.Location.Coordinates.Length == 2 ? s.Location.Coordinates[0] : 0,
                Pricing = new PricingDto
                {
                    Model = s.Pricing.Model,
                    Base = s.Pricing.Base,
                    PerHour = s.Pricing.PerHour,
                    PerKwh = s.Pricing.PerKwh,
                    TaxPct = s.Pricing.TaxPct
                },
                AvailabilitySummary = summary
            };
        }

        public static StationScheduleResponse ToResponse(this StationSchedule s)
        {
            return new StationScheduleResponse
            {
                Weekly = new WeeklyScheduleDto
                {
                    Mon = s.Weekly.Mon.Select(x => new DayTimeRangeDto { Start = x.Start, End = x.End }).ToList(),
                    Tue = s.Weekly.Tue.Select(x => new DayTimeRangeDto { Start = x.Start, End = x.End }).ToList(),
                    Wed = s.Weekly.Wed.Select(x => new DayTimeRangeDto { Start = x.Start, End = x.End }).ToList(),
                    Thu = s.Weekly.Thu.Select(x => new DayTimeRangeDto { Start = x.Start, End = x.End }).ToList(),
                    Fri = s.Weekly.Fri.Select(x => new DayTimeRangeDto { Start = x.Start, End = x.End }).ToList(),
                    Sat = s.Weekly.Sat.Select(x => new DayTimeRangeDto { Start = x.Start, End = x.End }).ToList(),
                    Sun = s.Weekly.Sun.Select(x => new DayTimeRangeDto { Start = x.Start, End = x.End }).ToList()
                },
                Exceptions = s.Exceptions.Select(e => new ScheduleExceptionDto { Date = e.Date, Closed = e.Closed }).ToList(),
                CapacityOverrides = s.CapacityOverrides.Select(c => new CapacityOverrideDto { Date = c.Date, Connectors = c.Connectors }).ToList(),
                UpdatedAtUtc = s.UpdatedAtUtc
            };
        }
                public static AdminFullStationDto ToAdminFullStationDto(this Station s)
        {
            return new AdminFullStationDto
            {
                Id = s.Id!,
                Name = s.Name,
                Type = s.Type,
                Connectors = s.Connectors,
                Status = s.Status,
                AutoApproveEnabled = s.AutoApproveEnabled,
                BackOfficeNic = s.BackOfficeNic,
                Location = new GeoPointDto
                {
                    Type = s.Location?.Type ?? "Point",
                    Coordinates = s.Location?.Coordinates ?? Array.Empty<double>()
                },
                DefaultSlotMinutes = s.DefaultSlotMinutes,
                Pricing = new PricingAdminDto
                {
                    Model = s.Pricing.Model,
                    Base = s.Pricing.Base,
                    PerHour = s.Pricing.PerHour,
                    PerKwh = s.Pricing.PerKwh,
                    TaxPct = s.Pricing.TaxPct
                },
                HoursTimezone = s.HoursTimezone,
                CreatedAtUtc = s.CreatedAtUtc,
                UpdatedAtUtc = s.UpdatedAtUtc,
                UpdatedBy = s.UpdatedBy
            };
        }
    }
}