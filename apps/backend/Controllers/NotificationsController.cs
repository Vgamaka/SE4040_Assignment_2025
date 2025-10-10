using System.Security.Claims;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace EvCharge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Owner/Operator/Admin — data is scoped by NIC
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notifications;

        public NotificationsController(INotificationService notifications)
        {
            _notifications = notifications;
        }

        // GET /api/Notifications?unreadOnly=true&page=1&pageSize=20
        [HttpGet]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> Mine(
            [FromQuery] bool? unreadOnly = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var nic = User.FindFirst(ClaimTypes.NameIdentifier)?.Value   // standard
                      ?? User.FindFirst("sub")?.Value                    // JWT 'sub'
                      ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nic)) return Unauthorized();

            var (docs, total) = await _notifications.ListMineAsync(nic, unreadOnly, page, pageSize, ct);

            var items = docs.Select(d => new NotificationListItem
            {
                Id = d.Id!,
                Type = d.Type,
                Subject = d.Subject,
                Message = d.Message,
                Payload = d.Payload?.ToDictionary(), // helper below
                CreatedAtUtc = d.CreatedAtUtc,
                ReadAtUtc = d.ReadAtUtc
            }).ToList();

            return Ok(new { total, page, pageSize, items });
        }

        // PUT /api/Notifications/{id}/read
        [HttpPut("{id}/read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkRead([FromRoute] string id, CancellationToken ct)
        {
            var nic = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value
                      ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nic)) return Unauthorized();

            var ok = await _notifications.MarkReadAsync(id, nic, ct);
            if (!ok) return NotFound(new { error = "NotFoundOrAlreadyRead", message = "Notification not found or already read." });

            return NoContent();
        }

        // PUT /api/Notifications/read-all
        [HttpPut("read-all")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        {
            var nic = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value
                      ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nic)) return Unauthorized();

            var changed = await _notifications.MarkAllReadAsync(nic, ct);
            return Ok(new { updated = changed });
        }
    }

    // Small helper to convert BsonDocument -> Dictionary<string, object?>
    internal static class BsonExtensions
    {
        public static Dictionary<string, object?> ToDictionary(this BsonDocument doc)
        {
            var d = new Dictionary<string, object?>();
            foreach (var el in doc.Elements)
            {
                d[el.Name] = el.Value?.IsBsonNull == true ? null : BsonTypeMapper.MapToDotNetValue(el.Value);
            }
            return d;
        }
    }
}
