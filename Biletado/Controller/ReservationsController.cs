using Biletado.Models;
using Microsoft.AspNetCore.Mvc;

namespace Biletado.Controller;

[Route("api/v3/reservations/reservations")]
[ApiController]
public class ReservationsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAllReservations()
    {
        return Ok();
    }

    [HttpPost]
    public IActionResult CreateReservation([FromBody] Reservation reservation)
    {
        return Ok();
    }

    [HttpGet("{id}")]
    public IActionResult GetReservationById(int id)
    {
        return Ok();
    }

    [HttpPut("{id}")]
    public IActionResult UpdateReservation(int id, [FromBody] Reservation reservation)
    {
        return Ok();
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteReservation(int id)
    {
        return Ok();
    }
    
}