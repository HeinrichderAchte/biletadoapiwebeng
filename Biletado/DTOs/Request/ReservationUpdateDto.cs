using System;
using System.Text.Json.Serialization;

namespace Biletado.DTOs.Request
{
    // Helper container that records presence and parsed value of deletedAt
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
        public DeletedAtField deletedAt { get; set; } = new DeletedAtField();
    }
}

