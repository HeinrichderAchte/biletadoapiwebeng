using System;
using System.Text.Json.Serialization;

namespace Biletado.DTOs.Request
{
    [JsonConverter(typeof(DeletedAtFieldJsonConverter))]
    public class DeletedAtField
    {
        public bool Present { get; set; } = false;
        public DateTime? Value { get; set; } = null;
    }

    public class ReservationUpdateDto
    {
        public Guid? reservationId { get; set; }
        public DateTime? fromDate { get; set; }
        public DateTime? toDate { get; set; }
        public Guid? roomId { get; set; }
        public DeletedAtField? deletedAt { get; set; } = null;
    }
}
