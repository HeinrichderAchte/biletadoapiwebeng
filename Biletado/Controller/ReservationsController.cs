using System.Text.Json;
using Biletado.Models;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;
using Biletado.DTOs.Response;
using Biletado.Persistence.Contexts;
using Biletado.Services;
using Biletado.Utils;
using Microsoft.EntityFrameworkCore;

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
        [FromBody] JsonElement? body = null)
    {
        var rawAction = HttpContext.Request.Query["action"].FirstOrDefault();
    _logger.LogInformation("UpdateReservation called. BoundAction={BoundAction} RawQueryAction={RawAction} BodyPresent={BodyPresent}", action, rawAction, body.HasValue);

    if (!string.IsNullOrEmpty(rawAction) && Enum.TryParse<ActionType>(rawAction, true, out var parsed))
    {
        action = parsed;
        _logger.LogInformation("Parsed action from raw query: {Action}", action);
    }

    var existing = await _db.Reservations.FindAsync(id);

    bool bodyHasDeletedAt = false;
    if (body.HasValue && body.Value.ValueKind != JsonValueKind.Null && body.Value.TryGetProperty("deletedAt", out var deletedAtProp))
    {
        bodyHasDeletedAt = true;
        var validationError = DeletedAtValidator.ValidateJsonProperty(deletedAtProp);
        if (validationError != null)
        {
            return BadRequest(new { error = validationError });
        }
    }

    // Präsenz von deletedAt nur als Restore behandeln, wenn die Reservierung existiert UND aktuell gelöscht ist.
    // Wenn die Reservierung existiert aber NICHT gelöscht ist, das Restore-Signal ignorieren und zur Replace/Validation weitergehen.
    // Wenn die Reservierung nicht existiert, fällt die Anfrage zur Create/Replace-Logik durch.
    if (action == ActionType.Restore || bodyHasDeletedAt)
    {
        if (existing != null)
        {
            if (existing.deletedAt == null)
            {
                if (action == ActionType.Restore)
                {
                    // explizites Restore-Action, aber Objekt nicht gelöscht -> Fehler
                    return BadRequest(new { error = "Reservation is not deleted, cannot restore" });
                }
                // bodyHasDeletedAt, aber Objekt ist nicht gelöscht -> IGNORIEREN und weiter zur Replace/Validation
            }
            else
            {
                // existiert und ist gelöscht -> restore durchführen
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
        // existing == null -> weiter zur Create/Replace-Logik (neue Reservierung anlegen)
    }

    // Replace/create path requires body
    if (!body.HasValue || body.Value.ValueKind == JsonValueKind.Null)
    {
        return BadRequest(new { error = "Reservation body is required for replace/create action" });
    }

    Reservation? reservationFromBody = null;
    try
    {
        reservationFromBody = JsonSerializer.Deserialize<Reservation>(body.Value.GetRawText());
    }
    catch (System.Exception ex)
    {
        _logger.LogWarning(ex, "Failed to deserialize reservation body");
        return BadRequest(new { error = "Invalid reservation body" });
    }

    if (reservationFromBody == null) return BadRequest(new { error = "Reservation body is required for replace/create action" });

    var errors = new List<ErrorDetail>();

    // id consistency
    if (reservationFromBody.reservationId != Guid.Empty && reservationFromBody.reservationId != id)
    {
        errors.Add(new ErrorDetail("bad_request", "reservationId in body must match route id"));
    }
    reservationFromBody.reservationId = id;

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
            // ensure deletedAt is null for new reservations (client may have sent deletedAt)
            reservationFromBody.deletedAt = null;

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
                return Ok(existing);
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

