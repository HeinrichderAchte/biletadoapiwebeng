using Biletado.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

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
    
    public ReservationsController(ReservationsDbContext db, ILogger<ReservationsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Liefert alle Reservierungen (mit optionalen Filtern).
    /// </summary>
    /// <remarks>If include_deleted is set to true, deleted reservations will be included.</remarks>
    /// <param name="includeDeleted">Set this true to include deleted reservations.</param>
    /// <param name="roomId">Filter the returned reservation by the given room (UUID).</param>
    /// <param name="before">Filter for reservations where reservation.from &lt; before (ISO date).</param>
    /// <param name="after">Filter for reservations where reservation.to &gt; after (ISO date).</param>
    [HttpGet]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "List reservations", Description = "List reservations with optional filters. If include_deleted=true, deleted reservations are included.")]
    public async Task <IActionResult> GetAllReservations(
        [FromQuery(Name = "include_deleted")] [SwaggerParameter("Set this true to include deleted reservations.")] bool includeDeleted = false,
        [FromQuery(Name = "room_id")] [SwaggerParameter("Filter the returned reservation by the given room (UUID). ")] Guid? roomId = null,
        [FromQuery(Name = "before")] [SwaggerParameter("Filter for reservations where reservation.from < before (ISO date).")][SwaggerSchema(Format = "date")] DateTime? before = null,
        [FromQuery(Name = "after")] [SwaggerParameter("Filter for reservations where reservation.to > after (ISO date).")][SwaggerSchema(Format = "date")] DateTime? after = null
    )
    {
        try
        {
            // convert before/after to DateOnly constants outside the EF expression so EF can translate comparisons
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
            // Typical for missing connection string
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
        if (reservation.reservationId == Guid.Empty) reservation.reservationId = Guid.NewGuid();

        try
        {
            await _db.Reservations.AddAsync(reservation);
            await _db.SaveChangesAsync();

            // Audit log
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

    /// <summary>
    /// Update a reservation: replace fields or restore a deleted reservation.
    /// </summary>
    /// <param name="id">Reservation id</param>
    /// <param name="action">Action to perform: 'Replace' (default) or 'Restore' to undelete</param>
    /// <param name="reservation">Reservation body for replace action (required when action=Replace)</param>
    [HttpPut("{id}")]
    [Authorize]
    [SwaggerOperation(Summary = "Update or restore a reservation")]
    public async Task<IActionResult> UpdateReservation(
        Guid id,
        [FromQuery(Name = "action")] [SwaggerParameter("Action to perform: 'Replace' (default) or 'Restore' to undelete")] ActionType action = ActionType.Replace,
        [FromBody] Reservation? reservation = null)
    {
        var existing = await _db.Reservations.FindAsync(id);
        if (existing == null) return NotFound();

        if (action == ActionType.Restore)
        {
            if (existing.deletedAt == null)
            {
                // already active
                return Ok(existing);
            }
            existing.deletedAt = null;
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
        else // Replace
        {
            if (reservation == null) return BadRequest(new { error = "Reservation body is required for replace action" });

            // Replace fields (keep the id from the route)
            existing.fromDate = reservation.fromDate;
            existing.toDate = reservation.toDate;
            existing.roomId = reservation.roomId;
            existing.deletedAt = reservation.deletedAt;

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

