using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvCharge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "BackOffice,Admin")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportsService _svc;
        public ReportsController(IReportsService svc) { _svc = svc; }

        /// <summary>Summary KPIs for the given window, optionally scoped to a station.</summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(SummaryReportResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Summary([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] string? stationId, CancellationToken ct)
        {
            var res = await _svc.GetSummaryAsync(fromUtc, toUtc, stationId, ct);
            return Ok(res);
        }

        /// <summary>Booking/time funnel time-series: metric=created|approved|rejected|cancelled|checkedin|completed; granularity=day|week|month.</summary>
        [HttpGet("time-series/bookings")]
        [ProducesResponseType(typeof(TimeSeriesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BookingTimeSeries(
            [FromQuery] string metric = "created",
            [FromQuery] string? stationId = null,
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] string granularity = "day",
            CancellationToken ct = default)
        {
            if (fromUtc is null || toUtc is null) return BadRequest(new { error = "InvalidRange", message = "fromUtc and toUtc are required." });
            if (fromUtc > toUtc) return BadRequest(new { error = "InvalidRange", message = "fromUtc must be <= toUtc." });

            var g = granularity?.Trim().ToLowerInvariant();
            if (g is not ("day" or "week" or "month"))
                return BadRequest(new { error = "InvalidGranularity", message = "granularity must be day|week|month." });

            var m = (metric ?? "created").Trim().ToLowerInvariant();
            if (m is not ("created" or "approved" or "rejected" or "cancelled" or "checkedin" or "completed"))
                return BadRequest(new { error = "InvalidMetric", message = "metric must be created|approved|rejected|cancelled|checkedin|completed." });

            var res = await _svc.GetBookingTimeSeriesAsync(m, stationId, fromUtc.Value, toUtc.Value, g!, ct);
            return Ok(res);
        }

        /// <summary>Revenue time-series grouped by granularity=day|week|month.</summary>
        [HttpGet("time-series/revenue")]
        [ProducesResponseType(typeof(TimeSeriesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RevenueTimeSeries(
            [FromQuery] string? stationId,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] string granularity = "day",
            CancellationToken ct = default)
        {
            if (fromUtc is null || toUtc is null) return BadRequest(new { error = "InvalidRange", message = "fromUtc and toUtc are required." });
            if (fromUtc > toUtc) return BadRequest(new { error = "InvalidRange", message = "fromUtc must be <= toUtc." });

            var g = granularity?.Trim().ToLowerInvariant();
            if (g is not ("day" or "week" or "month"))
                return BadRequest(new { error = "InvalidGranularity", message = "granularity must be day|week|month." });

            var res = await _svc.GetRevenueTimeSeriesAsync(stationId, fromUtc.Value, toUtc.Value, g!, ct);
            return Ok(res);
        }

        /// <summary>Daily station utilization (%), for local dates (yyyy-MM-dd) inclusive.</summary>
        [HttpGet("stations/{stationId}/utilization")]
        [ProducesResponseType(typeof(StationUtilizationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> StationUtilization([FromRoute] string stationId, [FromQuery] string fromLocal, [FromQuery] string toLocal, CancellationToken ct)
        {
            if (!DateOnly.TryParseExact(fromLocal, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from))
                return BadRequest(new { error = "InvalidFromLocal", message = "fromLocal must be yyyy-MM-dd." });
            if (!DateOnly.TryParseExact(toLocal, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
                return BadRequest(new { error = "InvalidToLocal", message = "toLocal must be yyyy-MM-dd." });
            if (from > to) return BadRequest(new { error = "InvalidRange", message = "fromLocal must be <= toLocal." });

            var res = await _svc.GetStationUtilizationAsync(stationId, from, to, ct);
            return Ok(res);
        }

        /// <summary>Revenue leaderboard by station.</summary>
        [HttpGet("revenue/by-station")]
        [ProducesResponseType(typeof(RevenueByStationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RevenueByStation([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken ct)
        {
            if (fromUtc is null || toUtc is null) return BadRequest(new { error = "InvalidRange", message = "fromUtc and toUtc are required." });
            if (fromUtc > toUtc) return BadRequest(new { error = "InvalidRange", message = "fromUtc must be <= toUtc." });

            var res = await _svc.GetRevenueByStationAsync(fromUtc.Value, toUtc.Value, ct);
            return Ok(res);
        }

        /// <summary>Station occupancy heatmap by (day-of-week x hour) in station local time.</summary>
        [HttpGet("stations/{stationId}/occupancy-heatmap")]
        [ProducesResponseType(typeof(OccupancyHeatmapResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> OccupancyHeatmap([FromRoute] string stationId, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken ct)
        {
            if (fromUtc is null || toUtc is null) return BadRequest(new { error = "InvalidRange", message = "fromUtc and toUtc are required." });
            if (fromUtc > toUtc) return BadRequest(new { error = "InvalidRange", message = "fromUtc must be <= toUtc." });

            var res = await _svc.GetOccupancyHeatmapAsync(stationId, fromUtc.Value, toUtc.Value, ct);
            return Ok(res);
        }
    }
}
