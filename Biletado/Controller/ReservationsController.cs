using System.Text.Json;
using Biletado.Models;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;
using Biletado.DTOs.Response;
using Biletado.Persistence.Contexts;
using Biletado.Services;
using Microsoft.EntityFrameworkCore;
using Biletado.DTOs.Request;

namespace Biletado.Controller;

public enum ActionType
{
    Replace,
    Restore
}

[Route("api/v3/reservations/reservations")]
[ApiController]
public class ReservationsController : ControllerBase
{
    private readonly ReservationsDbContext _db;
    private readonly ILogger<ReservationsController> _logger;
    private readonly IReservationService _reservationService;
    
    public ReservationsController(ReservationsDbContext db, ILogger<ReservationsController> logger, IReservationService reservationService)
    {
        _db = db;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reservationService = reservationService ?? throw new ArgumentNullException(nameof(reservationService));
    }


    [HttpGet]
    [AllowAnonymous]
    public async Task <IActionResult> GetAllReservations(
        [FromQuery(Name = "include_deleted")] [SwaggerParameter("Set this true to include deleted reservations.")] bool includeDeleted = false,
        [FromQuery(Name = "room_id")] [SwaggerParameter("Filter the returned reservation by the given room (UUID). ")] Guid? roomId = null,
        [FromQuery(Name = "before")] [SwaggerParameter("Filter for reservations where reservation.from < before (ISO date).")][SwaggerSchema(Format = "date")] DateTime? before = null,
        [FromQuery(Name = "after")] [SwaggerParameter("Filter for reservations where reservation.to > after (ISO date).")][SwaggerSchema(Format = "date")] DateTime? after = null
    )
    {
        try
        {
            
            _logger.LogInformation("Listing reservations"); 
            DateOnly? beforeDate = before.HasValue ? DateOnly.FromDateTime(before.Value) : (DateOnly?)null;
            DateOnly? afterDate = after.HasValue ? DateOnly.FromDateTime(after.Value) : (DateOnly?)null;

            var q = _db.Reservations.AsQueryable();

            if (!includeDeleted)
            {
                q = q.Where(r => r.deletedAt == null);
            }

            if (roomId.HasValue)
            {
                q = q.Where(r => r.roomId == roomId);
            }

            if (beforeDate.HasValue)
            {
                var bd = beforeDate.Value;
                q = q.Where(r => r.fromDate.HasValue && r.fromDate.Value < bd);
            }

            if (afterDate.HasValue)
            {
                var ad = afterDate.Value;
                q = q.Where(r => r.toDate.HasValue && r.toDate.Value > ad);
            }

            var reservations = await q.ToListAsync();
            return Ok(reservations);
        }
        catch (System.InvalidOperationException ex)
        {
            _logger.LogError(ex, "Database operation failed (likely missing connection string)");
            return StatusCode(503, new { error = "Database not configured or unreachable", detail = ex.Message });
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while fetching reservations");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReservation([FromBody] Reservation reservation)
    {
        if (reservation == null) return BadRequest();
        
        _logger.LogInformation("Create reservation request received");
        try
        {
            var errors = new List<ErrorDetail>(); 
            if (reservation.reservationId == Guid.Empty)
            {
                errors.Add(new ErrorDetail("bad_request", "reservationId is required and must be a valid UUID."));
            }
            
            if(reservation.fromDate > reservation.toDate)
            {
                errors.Add(new ErrorDetail("bad_request", "fromDate must be earlier than toDate."));
            }

            if (!reservation.roomId.HasValue)
            {
                errors.Add(new ErrorDetail("bad_request", "roomId is required and must be valid UUID"));
            }
            else
            {
                var roomExists = await _reservationService.RoomExists(reservation.roomId.Value);
                if (!roomExists)
                {
                    errors.Add(new ErrorDetail("bad_request", $"roomId {reservation.roomId} does not exist."));
                }
                else
                {
                    var from = reservation.fromDate;
                    var to = reservation.toDate;
                    if (from.HasValue && to.HasValue)
                    {
                        var overlapExists = await _db.Reservations.AnyAsync(r =>
                            r.deletedAt == null &&
                            r.roomId == reservation.roomId &&
                            r.fromDate.HasValue && r.toDate.HasValue &&
                            !(r.toDate.Value < from.Value || r.fromDate.Value > to.Value)
                        );

                        if (overlapExists)
                        {
                            errors.Add(new ErrorDetail("conflict", "Reservation already exists"));
                        }
                    }
                }
            }
            if (errors.Count > 0)
            {
                _logger.LogWarning("Validation errors while creating reservation: {Errors}", errors);
                return BadRequest(new { errors });
            }
            
            var existsWithSameId = await _db.Reservations.AnyAsync(r => r.reservationId == reservation.reservationId);
            if (existsWithSameId)
            {
                _logger.LogWarning("Attempt to create duplicate reservationId {ReservationId}", reservation.reservationId);
                return Conflict(new { error = "Reservation with same reservationId already exists." });
            }
            
            await _db.Reservations.AddAsync(reservation);
            await _db.SaveChangesAsync();

            
            var userId = User.Identity?.Name ?? "anonymous";
            _logger.LogInformation("Audit: Operation={Operation} ObjectType={ObjectType} ObjectId={ObjectId} UserId={UserId}", "Create", "Reservation", reservation.reservationId, userId);
            
        
            
            return CreatedAtAction(nameof(GetReservationById), new { id = reservation.reservationId }, reservation);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to create reservation");
            return StatusCode(500, new { error = "Failed to persist reservation" });
        }
    }

    

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReservationById([FromRoute]Guid id)
    {
        var res = await _db.Reservations.FindAsync(id);
        if (res == null) return NotFound();
        return Ok(res);
    }
    
    [HttpPut("{id}")]
    [Authorize]
    [SwaggerOperation(Summary = "Update or restore a reservation")]
    public async Task<IActionResult> UpdateReservation(
        Guid id,
        [FromQuery(Name = "action")] [SwaggerParameter("Action to perform: 'Replace' (default) or 'Restore' to undelete")] ActionType action = ActionType.Replace,
        [FromBody] ReservationUpdateDto? dto = null)
    {
        var rawAction = HttpContext.Request.Query["action"].FirstOrDefault();
        _logger.LogInformation("UpdateReservation called. BoundAction={BoundAction} RawQueryAction={RawAction} BodyPresent={BodyPresent}", action, rawAction, dto != null);

        if (!string.IsNullOrEmpty(rawAction) && Enum.TryParse<ActionType>(rawAction, true, out var parsed))
        {
            action = parsed;
            _logger.LogInformation("Parsed action from raw query: {Action}", action);
        }

        var existing = await _db.Reservations.FindAsync(id);

        // if no body provided for replace/create -> bad request
        if (dto == null)
        {
            return BadRequest(new { error = "Reservation body is required for replace/create action" });
        }

        // If dto.deletedAt property was present, treat as restore signal only when reservation exists and deleted
        // Interpret deletedAt presence as restore signal only when it contains a timestamp:
        // - dto.deletedAt == null -> client did not supply field
        // - dto.deletedAt != null && dto.deletedAt.Value == null -> explicit null -> treat as restore signal
        // - dto.deletedAt != null && dto.deletedAt.Value != null -> client supplied a timestamp (not allowed for replace/create)
        if (dto.deletedAt != null && dto.deletedAt.Value != null)
        {
            // timestamp present -> restore signal
            if (existing != null)
            {
                if (existing.deletedAt == null)
                {
                    // reservation exists but not deleted -> cannot restore
                    if (action == ActionType.Restore)
                    {
                        return BadRequest(new { error = "Reservation is not deleted, cannot restore" });
                    }
                    // else ignore restore signal and continue to replace validation
                }
                else
                {
                    // restore existing (set deletedAt to null)
                    existing.deletedAt = null;
                    _db.Entry(existing).Property(e => e.deletedAt).IsModified = true;
                    try
                    {
                        await _db.SaveChangesAsync();
                        var userId = User.Identity?.Name ?? "anonymous";
                        _logger.LogInformation("Audit: Operation={Operation} ObjectType={ObjectType} ObjectId={ObjectId} UserId={UserId}", "Restore", "Reservation", existing.reservationId, userId);
                        return Ok(existing);
                    }
                    catch (System.Exception ex)
                    {
                        _logger.LogError(ex, "Failed to restore reservation");
                        return StatusCode(500, new { error = "Failed to restore reservation" });
                    }
                }
            }
            // if existing == null => fall through to create/replace logic (allow creation)
        }

        // At this point handle replace/create
        var reservationFromBody = new Reservation
        {
            reservationId = dto.reservationId ?? id,
            fromDate = dto.fromDate.HasValue ? DateOnly.FromDateTime(dto.fromDate.Value) : (DateOnly?)null,
            toDate = dto.toDate.HasValue ? DateOnly.FromDateTime(dto.toDate.Value) : (DateOnly?)null,
            roomId = dto.roomId,
            deletedAt = null // clients cannot set deletedAt on create/replace; restore controlled via presence
        };

        var errors = new List<ErrorDetail>();

        // id consistency
        if (dto.reservationId.HasValue && dto.reservationId.Value != id)
        {
            errors.Add(new ErrorDetail("bad_request", "reservationId in body must match route id"));
        }

        // from/to validation
        if (reservationFromBody.fromDate.HasValue && reservationFromBody.toDate.HasValue && reservationFromBody.fromDate > reservationFromBody.toDate)
        {
            errors.Add(new ErrorDetail("bad_request", "fromDate must be earlier than toDate."));
        }

        // room validation
        if (!reservationFromBody.roomId.HasValue)
        {
            errors.Add(new ErrorDetail("bad_request", "roomId is required and must be a valid UUID"));
        }
        else
        {
            var roomExists = await _reservationService.RoomExists(reservationFromBody.roomId.Value);
            if (!roomExists)
            {
                errors.Add(new ErrorDetail("bad_request", $"roomId {reservationFromBody.roomId} does not exist."));
            }
            else
            {
                // overlap check: exclude the current reservation id
                if (reservationFromBody.fromDate.HasValue && reservationFromBody.toDate.HasValue)
                {
                    var from = reservationFromBody.fromDate.Value;
                    var to = reservationFromBody.toDate.Value;

                    var overlapExists = await _db.Reservations.AnyAsync(r =>
                        r.deletedAt == null &&
                        r.roomId == reservationFromBody.roomId &&
                        r.reservationId != id &&
                        r.fromDate.HasValue && r.toDate.HasValue &&
                        !(r.toDate.Value < from || r.fromDate.Value > to)
                    );

                    if (overlapExists)
                    {
                        errors.Add(new ErrorDetail("conflict", "Reservation overlaps with an existing reservation in the same room."));
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validation errors while updating/creating reservation: {Errors}", errors);
            return BadRequest(new { errors });
        }

        // If no existing reservation found => create new
        if (existing == null)
        {
            try
            {
                // ensure deletedAt is null for new reservations
                reservationFromBody.deletedAt = null;

                var duplicate = await _db.Reservations.AnyAsync(r => r.reservationId == reservationFromBody.reservationId);
                if (duplicate)
                {
                    _logger.LogWarning("Attempt to create duplicate reservationId {ReservationId} in UpdateReservation", reservationFromBody.reservationId);
                    return Conflict(new { error = "Reservation with same reservationId already exists." });
                }

                await _db.Reservations.AddAsync(reservationFromBody);
                await _db.SaveChangesAsync();

                var userId = User.Identity?.Name ?? "anonymous";
                _logger.LogInformation("Audit: Operation={Operation} ObjectType={ObjectType} ObjectId={ObjectId} UserId={UserId}", "Create", "Reservation", reservationFromBody.reservationId, userId);

                return CreatedAtAction(nameof(GetReservationById), new { id = reservationFromBody.reservationId }, reservationFromBody);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to create reservation in UpdateReservation path");
                return StatusCode(500, new { error = "Failed to create reservation" });
            }
        }

        // existing != null => perform update (replace)
        existing.fromDate = reservationFromBody.fromDate;
        existing.toDate = reservationFromBody.toDate;
        existing.roomId = reservationFromBody.roomId;

        try
        {
            await _db.SaveChangesAsync();
            var userId = User.Identity?.Name ?? "anonymous";
            _logger.LogInformation("Audit: Operation={Operation} ObjectType={ObjectType} ObjectId={ObjectId} UserId={UserId}", "Replace", "Reservation", existing.reservationId, userId);
            return Ok(existing);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to replace reservation");
            return StatusCode(500, new { error = "Failed to replace reservation" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    [SwaggerOperation(Summary = "Delete a reservation (soft) or hard-delete if requested")]
    public async Task<IActionResult> DeleteReservation(
        Guid id,
        [FromQuery(Name = "hard")] [SwaggerParameter("Set to true to perform a hard delete (permanent removal). Default is false (soft delete). ")] bool hard = false)
    {
        var existing = await _db.Reservations.FindAsync(id);
        if (existing == null) return NotFound();
        var errors = new List<ErrorDetail>();
        if (hard)
        {
            try
            {
                _db.Reservations.Remove(existing);
                await _db.SaveChangesAsync();

                var userId = User.Identity?.Name ?? "anonymous";
                _logger.LogInformation("Audit: Operation={Operation} ObjectType={ObjectType} ObjectId={ObjectId} UserId={UserId}", "HardDelete", "Reservation", existing.reservationId, userId);

                return NoContent();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to hard-delete reservation");
                return StatusCode(500, new { error = "Failed to hard-delete reservation" });
            }
        }
        else
        {
            // soft delete
            if (existing.deletedAt != null)
            {
                // already deleted
                errors.Add(new ErrorDetail("not_found", "Reservation already deleted.")); 
                return NotFound(errors);
            }
            existing.deletedAt = DateTime.UtcNow;
            try
            {
                await _db.SaveChangesAsync();

                var userId = User.Identity?.Name ?? "anonymous";
                _logger.LogInformation("Audit: Operation={Operation} ObjectType={ObjectType} ObjectId={ObjectId} UserId={UserId}", "SoftDelete", "Reservation", existing.reservationId, userId);

                return Ok(existing);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to soft-delete reservation");
                return StatusCode(500, new { error = "Failed to soft-delete reservation" });
            }
        }
    }
    
}

