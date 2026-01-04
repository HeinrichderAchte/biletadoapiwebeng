using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Biletado.Models;

public class Reservation
{
    public Guid reservationId { get; set; }
    public DateOnly? fromDate { get; set; }
    public DateOnly? toDate { get; set; }
    public Guid? roomId { get; set; }
    public DateTime? deletedAt { get; set; }

}