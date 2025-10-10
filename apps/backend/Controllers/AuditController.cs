using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace EvCharge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,BackOffice")]
    public class AuditsController : ControllerBase
    {
        private readonly IAuditService _svc;
        public AuditsController(IAuditService svc) { _svc = svc; }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResponse<AuditItemDto>>> Search([FromBody] AuditSearchRequest req, CancellationToken ct)
        {
            var (items, total) = await _svc.SearchAsync(
                req.EntityType, req.EntityId, req.Action, req.Actor,
                req.FromUtc, req.ToUtc, req.Page, req.PageSize, ct);

            var dto = new PagedResponse<AuditItemDto>
            {
                Page = req.Page,
                PageSize = req.PageSize,
                Total = total,
                Items = items.Select(e => new AuditItemDto
                {
                    Id = e.Id!,
                    EntityType = e.EntityType,
                    EntityId = e.EntityId,
                    Action = e.Action,
                    ActorNic = e.ActorNic,
                    ActorRole = e.ActorRole,
                    //  Map BsonValue -> .NET object safely
                    Payload = e.Payload is null
                        ? null
                        : e.Payload.Elements.ToDictionary(
                            el => el.Name,
                            el => BsonTypeMapper.MapToDotNetValue(el.Value)
                          ),
                    CreatedAtUtc = e.CreatedAtUtc
                }).ToList()
            };

            return Ok(dto);
        }
    }
}
