using ADO_EF.Data;
using ADO_EF.Data.Entities;
using ADO_EF.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ADO_EF
{
    public partial class BarberSelectionWindow : Window
    {
        private readonly DataContext _context;
        private readonly BookingService _bookingService;
        private readonly ILogger<BarberSelectionWindow> _logger;
        private DateTime _selectedDate = DateTime.Today;
        private Guid _selectedBarberId;
        private Guid _selectedServiceId;

        public BarberSelectionWindow(DataContext context, BookingService bookingService, ILogger<BarberSelectionWindow> logger, DateTime selectedDate, Guid selectedServiceId)
        {
            InitializeComponent();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _bookingService = bookingService ?? throw new ArgumentNullException(nameof(bookingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _selectedDate = selectedDate;
            _selectedServiceId = selectedServiceId;
            LoadBarbers();
        }

        private async void LoadBarbers()
        {
            try
            {
                var barbers = await _bookingService.GetBarbersAsync();
                if (barbers == null)
                {
                    barbers = new List<Barber>();
                }

                if (!barbers.Any())
                {
                    _logger.LogInformation("Таблица Barbers пуста, добавление тестовых данных.");
                    var testBarbers = new[]
                    {
                        new Barber { Id = Guid.NewGuid(), Name = "Олександр Моторний", Role = "Chief barber", Rating = 4.9, Reviews = 48, ImageSource = "/Resources/barber1.jpg" },
                        new Barber { Id = Guid.NewGuid(), Name = "Кіріл Шайкін", Role = "Барбер", Rating = 4.8, Reviews = 28, ImageSource = "/Resources/barber2.jpg" },
                        new Barber { Id = Guid.NewGuid(), Name = "Михайло Горшін", Role = "Chief barber", Rating = 4.7, Reviews = 25, ImageSource = "/Resources/barber3.jpg" },
                        new Barber { Id = Guid.NewGuid(), Name = "Анна Матвєєнко", Role = "Старший барбер", Rating = 4.6, Reviews = 15, ImageSource = "/Resources/barber4.jpg" },
                        new Barber { Id = Guid.NewGuid(), Name = "Антон Димарюк", Role = "Барбер", Rating = 4.5, Reviews = 10, ImageSource = "/Resources/barber5.jpg" }
                    };
                    _context.Barbers.AddRange(testBarbers);
                    await _context.SaveChangesAsync();
                    barbers.AddRange(testBarbers);
                }

                var barberViewModels = barbers.Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.Role,
                    b.Rating,
                    b.Reviews,
                    ImageSource = b.ImageSource ?? "/Resources/default_barber.jpg",
                    TimeSlots = GetAvailableTimeSlots(b.Id)
                }).ToList();

                BarberList.ItemsSource = barberViewModels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке списка барберов");
                MessageBox.Show($"Ошибка при загрузке барберов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<string> GetAvailableTimeSlots(Guid barberId)
        {
            var startTime = _selectedDate.Date.AddHours(10); 
            var endTime = _selectedDate.Date.AddHours(20);   
            var timeSlots = new List<string>();
            var bookings = _context.Bookings.Where(b => b.BarberId == barberId && b.BookingDateTime.Date == _selectedDate.Date).ToList();

            while (startTime < endTime)
            {
                if (!bookings.Any(b => b.BookingDateTime.TimeOfDay == startTime.TimeOfDay))
                {
                    timeSlots.Add(startTime.ToString("HH:mm"));
                }
                startTime = startTime.AddMinutes(30); 
            }

            return timeSlots;
        }

        private void TimeSlot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Content is string timeSlot)
            {
                _selectedBarberId = (Guid)((dynamic)BarberList.SelectedItem).Id;
                MessageBox.Show($"Вы выбрали барбера {_selectedBarberId} на время {timeSlot}.", "Выбор времени", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
        }
    }

    public partial class Barber
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public double Rating { get; set; }
        public int Reviews { get; set; }
        public string ImageSource { get; set; } = string.Empty;
    }
}