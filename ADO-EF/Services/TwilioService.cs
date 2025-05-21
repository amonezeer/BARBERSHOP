using ADO_EF.Data;
using ADO_EF.Data.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ADO_EF.Services
{
    public class TwilioService
    {
        private readonly TwilioSettings _settings;
        private readonly DataContext _context;
        private readonly ILogger<TwilioService> _logger;

        public TwilioService(IConfiguration configuration, DataContext context, ILogger<TwilioService> logger)
        {
            var settings = configuration.GetSection("Twilio").Get<TwilioSettings>();
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings), "Twilio settings are not configured in appsettings.json");
            }
            _settings = settings;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrEmpty(_settings.AccountSid) || string.IsNullOrEmpty(_settings.AuthToken) || string.IsNullOrEmpty(_settings.PhoneNumber))
            {
                throw new InvalidOperationException("Twilio settings are not properly configured.");
            }

            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);
        }

        private string GenerateVerificationCode()
        {
            Random random = new Random();
            return string.Join("", Enumerable.Range(0, 6).Select(_ => random.Next(0, 10).ToString()));
        }

        public async Task<SendVerificationResult> SendVerificationCode(User user, string phoneNumber)
        {
            try
            {
                _logger.LogInformation($"Инициализация Twilio с AccountSid: {_settings.AccountSid}, From: {_settings.PhoneNumber}, To: {phoneNumber}");

                string code = GenerateVerificationCode();

                var verificationCode = new VerificationCode
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Code = code,
                    CreatedAt = DateTime.UtcNow,
                    IsUsed = false,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                };

                user.Phonenumber = phoneNumber;

                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        _context.VerificationCodes.Add(verificationCode);
                        _context.Users.Update(user);
                        await _context.SaveChangesAsync();
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Ошибка при сохранении кода верификации в базе данных");
                        return new SendVerificationResult
                        {
                            Success = false,
                            ErrorMessage = "Ошибка при сохранении данных в базе данных."
                        };
                    }
                }

                _logger.LogInformation($"Отправка SMS с кодом {code} на номер {phoneNumber}");

                var message = await MessageResource.CreateAsync(
                    body: $"Your verification code is: {code}",
                    from: new PhoneNumber(_settings.PhoneNumber),
                    to: new PhoneNumber(phoneNumber)
                );

                if (message.Status == MessageResource.StatusEnum.Failed)
                {
                    _logger.LogError($"Не удалось отправить SMS. Статус: {message.Status}, Код ошибки: {message.ErrorCode}, Сообщение об ошибке: {message.ErrorMessage}");
                    return new SendVerificationResult
                    {
                        Success = false,
                        ErrorMessage = $"Не удалось отправить SMS. Код ошибки: {message.ErrorCode}, Сообщение: {message.ErrorMessage}"
                    };
                }

                _logger.LogInformation($"SMS успешно отправлено. Статус: {message.Status}, SID: {message.Sid}");
                return new SendVerificationResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при отправке SMS на номер {phoneNumber}");
                return new SendVerificationResult
                {
                    Success = false,
                    ErrorMessage = $"Ошибка при отправке SMS: {ex.Message}"
                };
            }
        }

        public async Task<bool> VerifyCodeAsync(Guid userId, string code)
        {
            try
            {
                var verification = await _context.VerificationCodes
                    .OrderByDescending(v => v.CreatedAt)
                    .FirstOrDefaultAsync(v => v.UserId == userId && v.Code == code && !v.IsUsed && v.ExpiresAt > DateTime.UtcNow);

                if (verification == null)
                {
                    _logger.LogWarning($"Код {code} недействителен или истек для пользователя {userId}");
                    return false;
                }

                verification.IsUsed = true;
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"Пользователь {userId} не найден при верификации кода {code}");
                    return false;
                }

                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        _context.VerificationCodes.Update(verification);
                        await _context.SaveChangesAsync();
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Ошибка при обновлении статуса кода верификации в базе данных");
                        return false;
                    }
                }

                _logger.LogInformation($"Код {code} успешно подтвержден для пользователя {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при верификации кода {code} для пользователя {userId}");
                return false;
            }
        }
    }

    public class SendVerificationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class TwilioSettings
    {
        public string AccountSid { get; set; } = string.Empty;
        public string AuthToken { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }
}