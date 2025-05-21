using ADO_EF.Data;
using ADO_EF.Data.Entities;
using ADO_EF.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using BCrypt.Net;
using ADO_EF.Data;

namespace ADO_EF
{
    public partial class MainWindow : Window
    {
        private readonly DataContext _context;
        private readonly DatabaseService _dbService;
        private readonly EmailService _emailService;
        private readonly TwilioService _twilioService;
        private readonly BookingService _bookingService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MainWindow> _logger;
        private string? _verificationCode;
        private string? _email;
        private string? _phoneNumber;
        private User? _currentUser;
        private UserAccess? _currentUserAccess;
        private bool _isCodeVerified = false;
        private string _verificationMethod = "Email";
        private User? _tempUser;

        public MainWindow()
        {
            InitializeComponent();

            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
            optionsBuilder.UseSqlServer(_configuration.GetConnectionString("LocalDb"));
            _context = new DataContext(optionsBuilder.Options) ?? throw new InvalidOperationException("Не удалось инициализировать контекст базы данных.");

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<MainWindow>();

            _dbService = new DatabaseService(_context) ?? throw new InvalidOperationException("Не удалось инициализировать DatabaseService.");
            _emailService = new EmailService(_configuration) ?? throw new InvalidOperationException("Не удалось инициализировать EmailService.");
            _twilioService = new TwilioService(_configuration, _context, loggerFactory.CreateLogger<TwilioService>()) ?? throw new InvalidOperationException("Не удалось инициализировать TwilioService.");
            _bookingService = new BookingService(_context) ?? throw new InvalidOperationException("Не удалось инициализировать BookingService.");
        }

        private void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Visibility = Visibility.Collapsed;
            AuthPanel.Visibility = Visibility.Visible;
        }

        private void CloseAuthButton_Click(object sender, RoutedEventArgs e)
        {
            AuthPanel.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
            ResetAuthPanels();
        }

        private void ResetAuthPanels()
        {
            LoginPanel.Visibility = Visibility.Visible;
            MethodSelectionPanel.Visibility = Visibility.Collapsed;
            EmailPanel.Visibility = Visibility.Collapsed;
            PhonePanel.Visibility = Visibility.Collapsed;
            CodePanel.Visibility = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Collapsed;
            WelcomePanel.Visibility = Visibility.Collapsed;
            if (_tempUser != null)
            {
                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {
                        _context.Users.Remove(_tempUser);
                        _context.SaveChanges();
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Ошибка при очистке временного пользователя");
                    }
                }
                _tempUser = null;
            }
            _isCodeVerified = false;
            _email = null;
            _phoneNumber = null;
            CodeBox1.Text = CodeBox2.Text = CodeBox3.Text = CodeBox4.Text = CodeBox5.Text = CodeBox6.Text = "";
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(LoginBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password))
                {
                    MessageBox.Show("Введите логин и пароль!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var user = await _dbService.AuthenticateAsync(LoginBox.Text, PasswordBox.Password);
                if (user != null)
                {
                    _currentUser = user;
                    _currentUserAccess = await _context.UserAccesses.FirstOrDefaultAsync(ua => ua.UserId == user.Id);
                    if (_currentUserAccess == null)
                    {
                        MessageBox.Show("Ошибка доступа пользователя!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    WelcomeMessage.Text = $"Добро пожаловать, {_currentUser.Name}!";
                    AuthPanel.Visibility = Visibility.Collapsed;
                    MainContent.Visibility = Visibility.Visible;
                    LoginPanel.Visibility = Visibility.Collapsed;
                    WelcomePanel.Visibility = Visibility.Visible;
                }
                else
                {
                    MessageBox.Show("Неверный логин или пароль!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при входе в систему");
                MessageBox.Show("Произошла ошибка. Пожалуйста, попробуйте снова.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToRegister_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            MethodSelectionPanel.Visibility = Visibility.Visible;
        }

        private void ProceedToMethod_Click(object sender, RoutedEventArgs e)
        {
            if (RegistrationMethodComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _verificationMethod = selectedItem.Content.ToString() == "Email" ? "Email" : "SMS";
                MethodSelectionPanel.Visibility = Visibility.Collapsed;

                if (_verificationMethod == "Email")
                {
                    EmailPanel.Visibility = Visibility.Visible;
                }
                else if (_verificationMethod == "SMS")
                {
                    PhonePanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                MessageBox.Show("Выберите метод регистрации!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void SendCode_Click(object sender, RoutedEventArgs e)
        {
            await SendEmailCode();
        }

        private async void SendSMSCode_Click(object sender, RoutedEventArgs e)
        {
            await SendSMSCode();
        }

        private async Task SendEmailCode()
        {
            _email = EmailBox.Text;
            if (string.IsNullOrWhiteSpace(_email) || !IsValidEmail(_email))
            {
                MessageBox.Show("Введите корректный email, например, example@domain.com", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (await _dbService.IsEmailRegisteredAsync(_email))
            {
                MessageBox.Show("Этот email уже зарегистрирован!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _verificationCode = _emailService.SendVerificationCode(_email);
                CodeBox1.Text = CodeBox2.Text = CodeBox3.Text = CodeBox4.Text = CodeBox5.Text = CodeBox6.Text = "";
                EmailPanel.Visibility = Visibility.Collapsed;
                CodePanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке кода на email");
                MessageBox.Show("Ошибка при отправке кода.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SendSMSCode()
        {
            _phoneNumber = PhoneNumberBox.Text;
            if (string.IsNullOrWhiteSpace(_phoneNumber) || !IsValidPhoneNumber(_phoneNumber))
            {
                MessageBox.Show("Введите корректный номер телефона в формате +1234567890!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Phonenumber == _phoneNumber);
            if (existingUser != null)
            {
                MessageBox.Show("Этот номер телефона уже зарегистрирован!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _tempUser = new User { Id = Guid.NewGuid(), Phonenumber = _phoneNumber };
                using (var transaction = _context.Database.BeginTransaction())
                {
                    _context.Users.Add(_tempUser);
                    await _context.SaveChangesAsync();
                    transaction.Commit();
                }

                var result = await _twilioService.SendVerificationCode(_tempUser, _phoneNumber);
                if (result.Success)
                {
                    var verificationCode = await _context.VerificationCodes
                        .OrderByDescending(vc => vc.CreatedAt)
                        .FirstOrDefaultAsync(vc => vc.UserId == _tempUser.Id);

                    if (verificationCode == null || string.IsNullOrEmpty(verificationCode.Code))
                    {
                        MessageBox.Show("Не удалось сгенерировать код подтверждения!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        using (var transaction = _context.Database.BeginTransaction())
                        {
                            _context.Users.Remove(_tempUser);
                            await _context.SaveChangesAsync();
                            transaction.Commit();
                        }
                        _tempUser = null;
                        return;
                    }

                    _verificationCode = verificationCode.Code;
                    CodeBox1.Text = CodeBox2.Text = CodeBox3.Text = CodeBox4.Text = CodeBox5.Text = CodeBox6.Text = "";
                    PhonePanel.Visibility = Visibility.Collapsed;
                    CodePanel.Visibility = Visibility.Visible;
                }
                else
                {
                    MessageBox.Show($"Ошибка при отправке SMS! {result.ErrorMessage}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    using (var transaction = _context.Database.BeginTransaction())
                    {
                        _context.Users.Remove(_tempUser);
                        await _context.SaveChangesAsync();
                        transaction.Commit();
                    }
                    _tempUser = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке SMS");
                MessageBox.Show("Произошла непредвиденная ошибка.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                if (_tempUser != null)
                {
                    using (var transaction = _context.Database.BeginTransaction())
                    {
                        _context.Users.Remove(_tempUser);
                        await _context.SaveChangesAsync();
                        transaction.Commit();
                    }
                    _tempUser = null;
                }
            }
        }

        private bool IsValidEmail(string email)
        {
            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, emailPattern);
        }

        private bool IsValidPhoneNumber(string phoneNumber)
        {
            return !string.IsNullOrWhiteSpace(phoneNumber) &&
                   phoneNumber.StartsWith("+") &&
                   phoneNumber.Length >= 10 &&
                   phoneNumber.Substring(1).All(char.IsDigit);
        }

        private void CodeBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!char.IsDigit(e.Text, 0))
            {
                e.Handled = true;
            }
        }

        private void CodeBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox currentBox || currentBox.Text.Length != 1) return;

            if (currentBox == CodeBox1) CodeBox2.Focus();
            else if (currentBox == CodeBox2) CodeBox3.Focus();
            else if (currentBox == CodeBox3) CodeBox4.Focus();
            else if (currentBox == CodeBox4) CodeBox5.Focus();
            else if (currentBox == CodeBox5) CodeBox6.Focus();
            else if (currentBox == CodeBox6)
            {
                VerifyCode_Click(this, new RoutedEventArgs());
            }
        }

        private async void VerifyCode_Click(object sender, RoutedEventArgs e)
        {
            string enteredCode = $"{CodeBox1.Text}{CodeBox2.Text}{CodeBox3.Text}{CodeBox4.Text}{CodeBox5.Text}{CodeBox6.Text}";

            if (string.IsNullOrWhiteSpace(enteredCode) || enteredCode.Length != 6)
            {
                MessageBox.Show("Введите 6-значный код!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isCodeValid = _verificationMethod == "Email" ? enteredCode == _verificationCode :
                _tempUser != null && await _twilioService.VerifyCodeAsync(_tempUser.Id, enteredCode);

            if (isCodeValid)
            {
                _isCodeVerified = true;
                ConfirmedContactBox.Text = _verificationMethod == "Email" ? _email : _phoneNumber;
                CodePanel.Visibility = Visibility.Collapsed;
                RegisterPanel.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Неверный или истекший код!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                if (_verificationMethod == "SMS" && _tempUser != null)
                {
                    using (var transaction = _context.Database.BeginTransaction())
                    {
                        _context.Users.Remove(_tempUser);
                        await _context.SaveChangesAsync();
                        transaction.Commit();
                    }
                    _tempUser = null;
                }
            }
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCodeVerified)
            {
                MessageBox.Show("Сначала необходимо подтвердить код верификации!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(UsernameBox.Text) ||
                string.IsNullOrWhiteSpace(NameBox.Text) ||
                string.IsNullOrWhiteSpace(PhoneBox.Text) ||
                string.IsNullOrWhiteSpace(RegPasswordBox.Password) ||
                string.IsNullOrWhiteSpace(ConfirmPasswordBox.Password))
            {
                MessageBox.Show("Заполните обязательные поля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (RegPasswordBox.Password != ConfirmPasswordBox.Password)
            {
                MessageBox.Show("Пароли не совпадают!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (await _dbService.IsUsernameRegisteredAsync(UsernameBox.Text))
            {
                MessageBox.Show("Этот логин уже занят!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Guid userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Name = NameBox.Text,
                Email = _email ?? string.Empty,
                Phonenumber = _phoneNumber ?? PhoneBox.Text,
                RegisteredAt = DateTime.Now
            };

            string salt = Guid.NewGuid().ToString();
            var userAccess = new UserAccess
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Login = UsernameBox.Text,
                Salt = salt,
                Dk = BCrypt.Net.BCrypt.HashPassword(RegPasswordBox.Password + salt),
                RoleId = "SelfRegistered"
            };

            try
            {
                if (_verificationMethod == "SMS" && _tempUser != null)
                {
                    _tempUser.Name = user.Name;
                    _tempUser.Email = user.Email;
                    _tempUser.Phonenumber = user.Phonenumber;
                    _tempUser.RegisteredAt = user.RegisteredAt;
                    await _dbService.RegisterUserAsync(_tempUser, userAccess);
                }
                else
                {
                    await _dbService.RegisterUserAsync(user, userAccess);
                }

                MessageBox.Show("Регистрация успешна! Войдите в систему.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                RegisterPanel.Visibility = Visibility.Collapsed;
                LoginPanel.Visibility = Visibility.Visible;
                _isCodeVerified = false;
                _tempUser = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при регистрации");
                MessageBox.Show("Ошибка при регистрации.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                if (_verificationMethod == "SMS" && _tempUser != null)
                {
                    using (var transaction = _context.Database.BeginTransaction())
                    {
                        _context.Users.Remove(_tempUser);
                        await _context.SaveChangesAsync();
                        transaction.Commit();
                    }
                    _tempUser = null;
                }
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || _currentUserAccess == null) return;

            _email = _currentUser.Email;
            _phoneNumber = _currentUser.Phonenumber;
            ConfirmedContactBox.Text = _verificationMethod == "Email" ? _email : _phoneNumber;
            UsernameBox.Text = _currentUserAccess.Login;
            NameBox.Text = _currentUser.Name;
            PhoneBox.Text = _currentUser.Phonenumber;
            RegPasswordBox.Password = "";
            ConfirmPasswordBox.Password = "";

            WelcomePanel.Visibility = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Visible;
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null) return;

            if (MessageBox.Show("Точно удалить пользователя?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _currentUser.Name = "";
                _currentUser.Email = "";
                _currentUser.Phonenumber = "";
                _currentUser.Birthdate = null;
                _currentUser.DeletedAt = DateTime.Now;

                try
                {
                    await _context.SaveChangesAsync();
                    MessageBox.Show("Данные удалены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    _currentUser = null;
                    _currentUserAccess = null;
                    WelcomePanel.Visibility = Visibility.Collapsed;
                    LoginPanel.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при удалении пользователя");
                    MessageBox.Show("Ошибка при удалении.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            EmailPanel.Visibility = Visibility.Collapsed;
            PhonePanel.Visibility = Visibility.Collapsed;
            CodePanel.Visibility = Visibility.Collapsed;
            MethodSelectionPanel.Visibility = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Visible;

            if (_tempUser != null)
            {
                using (var transaction = _context.Database.BeginTransaction())
                {
                    _context.Users.Remove(_tempUser);
                    _context.SaveChanges();
                    transaction.Commit();
                }
                _tempUser = null;
            }
        }

        private void BackToMethodFromEmail_Click(object sender, RoutedEventArgs e)
        {
            EmailPanel.Visibility = Visibility.Collapsed;
            MethodSelectionPanel.Visibility = Visibility.Visible;
        }

        private void BackToMethodFromPhone_Click(object sender, RoutedEventArgs e)
        {
            PhonePanel.Visibility = Visibility.Collapsed;
            MethodSelectionPanel.Visibility = Visibility.Visible;

            if (_tempUser != null)
            {
                using (var transaction = _context.Database.BeginTransaction())
                {
                    _context.Users.Remove(_tempUser);
                    _context.SaveChanges();
                    transaction.Commit();
                }
                _tempUser = null;
            }
        }

        private void BackToPreviousFromCodeButton_Click(object sender, RoutedEventArgs e)
        {
            CodePanel.Visibility = Visibility.Collapsed;
            if (_verificationMethod == "Email")
            {
                EmailPanel.Visibility = Visibility.Visible;
            }
            else if (_verificationMethod == "SMS")
            {
                PhonePanel.Visibility = Visibility.Visible;
            }
            CodeBox1.Text = CodeBox2.Text = CodeBox3.Text = CodeBox4.Text = CodeBox5.Text = CodeBox6.Text = "";
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            _currentUser = null;
            _currentUserAccess = null;
            WelcomePanel.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Visible;
        }

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            var paletteHelper = new PaletteHelper();
            var currentTheme = paletteHelper.GetTheme();
            var newTheme = currentTheme.GetBaseTheme() == BaseTheme.Dark ? "Light" : "Dark";
            ((App)Application.Current).ChangeTheme(newTheme);
        }


        private async void ChooseBarberButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bookingService = new BookingService(_context) ?? throw new InvalidOperationException("Не удалось инициализировать BookingService.");
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<BarberSelectionWindow>();
                var barberWindow = new BarberSelectionWindow(_context, bookingService, logger, DatePicker.SelectedDate ?? DateTime.Today, Guid.Empty);
                barberWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при открытии окна выбора барбера");
                MessageBox.Show("Ошибка при открытии окна выбора барбера.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ChooseDateTimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                AccountButton_Click(sender, e);
                return;
            }
            await ShowBookingPanel();
        }

        private async void ChooseServiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                AccountButton_Click(sender, e);
                return;
            }
            await ShowBookingPanel();
        }

        private async Task ShowBookingPanel()
        {
            BookingPanel.Visibility = Visibility.Visible;
            MainContent.Visibility = Visibility.Collapsed;
            AuthPanel.Visibility = Visibility.Collapsed;

            LoadingIndicator.Visibility = Visibility.Visible;
            try
            {
                var barbers = await _bookingService.GetBarbersAsync() ?? new List<Barber>();
                if (!barbers.Any())
                {
                    var barber1 = new Barber { Name = "Роман Орда" };
                    var barber2 = new Barber { Name = "Максим Руденко" };
                    _context.Barbers.AddRange(barber1, barber2);
                    await _context.SaveChangesAsync();
                    barbers = await _bookingService.GetBarbersAsync() ?? new List<Barber>();
                }

                var services = await _bookingService.GetServicesAsync() ?? new List<Service>();
                if (!services.Any())
                {
                    var service1 = new Service { Name = "Стрижка", Price = 300, DurationMinutes = 30 };
                    var service2 = new Service { Name = "Бритье", Price = 200, DurationMinutes = 20 };
                    _context.Services.AddRange(service1, service2);
                    await _context.SaveChangesAsync();
                    services = await _bookingService.GetServicesAsync() ?? new List<Service>();
                }

                BarberComboBox.Items.Clear();
                foreach (var barber in barbers)
                {
                    BarberComboBox.Items.Add(new ComboBoxItem { Content = barber.Name, Tag = barber.Id });
                }
                if (BarberComboBox.Items.Count > 0) BarberComboBox.SelectedIndex = 0;

                ServiceComboBox.Items.Clear();
                foreach (var service in services)
                {
                    ServiceComboBox.Items.Add(new ComboBoxItem { Content = $"{service.Name} ({service.Price} грн, {service.DurationMinutes} мин)", Tag = service.Id });
                }
                if (ServiceComboBox.Items.Count > 0) ServiceComboBox.SelectedIndex = 0;

                DatePicker.SelectedDate = DateTime.Today;
                TimeComboBox.Items.Clear();

                async void UpdateTimeSlots(object s, SelectionChangedEventArgs e)
                {
                    if (s is ComboBox && BarberComboBox.SelectedItem is ComboBoxItem selectedBarberItem && DatePicker.SelectedDate.HasValue)
                    {
                        LoadingIndicator.Visibility = Visibility.Visible;
                        var barberId = (Guid)selectedBarberItem.Tag;
                        var date = DatePicker.SelectedDate.Value;
                        var timeSlots = await _bookingService.GetAvailableTimeSlotsAsync(barberId, date) ?? new List<DateTime>();
                        TimeComboBox.Items.Clear();
                        foreach (var slot in timeSlots)
                        {
                            TimeComboBox.Items.Add(new ComboBoxItem { Content = slot.ToString("HH:mm"), Tag = slot });
                        }
                        if (TimeComboBox.Items.Count > 0) TimeComboBox.SelectedIndex = 0;
                        LoadingIndicator.Visibility = Visibility.Collapsed;
                    }
                }

                BarberComboBox.SelectionChanged += UpdateTimeSlots;
                DatePicker.SelectedDateChanged += (s, e) => UpdateTimeSlots(BarberComboBox, e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке панели бронирования");
                MessageBox.Show("Ошибка при загрузке данных бронирования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async void BookButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || BarberComboBox.SelectedItem is not ComboBoxItem barberItem ||
                ServiceComboBox.SelectedItem is not ComboBoxItem serviceItem ||
                TimeComboBox.SelectedItem is not ComboBoxItem timeItem ||
                !DatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Выберите все параметры бронирования или войдите в аккаунт!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadingIndicator.Visibility = Visibility.Visible;
            try
            {
                var barberId = (Guid)barberItem.Tag;
                var serviceId = (Guid)serviceItem.Tag;
                var bookingDateTime = (DateTime)timeItem.Tag;

                var success = await _bookingService.CreateBookingAsync(_currentUser.Id, barberId, serviceId, bookingDateTime);
                if (success)
                {
                    MessageBox.Show("Бронирование успешно создано!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    BookingPanel.Visibility = Visibility.Collapsed;
                    MainContent.Visibility = Visibility.Visible;
                }
                else
                {
                    MessageBox.Show("Не удалось создать бронирование. Слот занят или данные некорректны.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании бронирования");
                MessageBox.Show("Ошибка при бронировании.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BookingPanel.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
        }
    }
}