using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Biletado.Models;

public class Reservation
{
    public Guid reservationId;
    public DateOnly? fromDate;
    public DateOnly? toDate;
    public Guid? roomId;
    public DateTime? deletedAt;

}