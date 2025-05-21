using System;

namespace ADO_EF.Data.Entities
{
    public class Booking
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid BarberId { get; set; }
        public Guid ServiceId { get; set; }
        public DateTime BookingDateTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}