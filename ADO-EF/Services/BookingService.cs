using ADO_EF.Data;
using ADO_EF.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ADO_EF.Services
{
    public class BookingService
    {
        private readonly DataContext _context;

        public BookingService(DataContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Barber>> GetBarbersAsync()
        {
            return await _context.Barbers.ToListAsync();
        }

        public async Task<List<Service>> GetServicesAsync()
        {
            return await _context.Services.ToListAsync();
        }

        public async Task<List<DateTime>> GetAvailableTimeSlotsAsync(Guid barberId, DateTime date)
        {
            var startTime = date.Date.AddHours(9); 
            var endTime = date.Date.AddHours(18); 
            var timeSlots = new List<DateTime>();

            while (startTime < endTime)
            {
                var existingBooking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.BarberId == barberId && b.BookingDateTime == startTime);
                if (existingBooking == null)
                {
                    timeSlots.Add(startTime);
                }
                startTime = startTime.AddMinutes(30); 
            }

            return timeSlots;
        }

        public async Task<bool> CreateBookingAsync(Guid userId, Guid barberId, Guid serviceId, DateTime bookingDateTime)
        {
            var existingBooking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BarberId == barberId && b.BookingDateTime == bookingDateTime);
            if (existingBooking != null)
            {
                return false;
            }

            var booking = new Booking
            {
                UserId = userId,
                BarberId = barberId,
                ServiceId = serviceId,
                BookingDateTime = bookingDateTime
            };

            await _context.Bookings.AddAsync(booking);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}