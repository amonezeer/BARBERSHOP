using System;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ADO_EF.Services
{
    public class EmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;

        public EmailService(IConfiguration configuration)
        {
            _smtpHost = configuration["EmailSettings:SmtpHost"];
            _smtpPort = int.Parse(configuration["EmailSettings:SmtpPort"]);
            _smtpUser = configuration["EmailSettings:SmtpUser"];
            _smtpPass = configuration["EmailSettings:SmtpPass"];

            if (string.IsNullOrEmpty(_smtpHost) || string.IsNullOrEmpty(_smtpUser) || string.IsNullOrEmpty(_smtpPass))
            {
                throw new InvalidOperationException("Email settings are not properly configured in appsettings.json or secrets.json.");
            }
        }

        public string SendVerificationCode(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email не может быть пустым.", nameof(email));
            }

            try
            {
                var code = new Random().Next(100000, 999999).ToString();

                var mailMessage = new MimeMessage();
                mailMessage.From.Add(new MailboxAddress("SwiftTalk", _smtpUser));
                mailMessage.To.Add(new MailboxAddress("", email));
                mailMessage.Subject = "Код для регистрации в SwiftTalk";

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <div style='text-align: center;'>
                        <h1 style='color: #4A90E2;'>SwiftTalk</h1>
                        </div>
                        <p>Здравствуйте,</p>
                        <p>Чтобы активировать ваш новый аккаунт SwiftTalk, скопируйте и вставьте этот 6-значный код подтверждения в приложение:</p>
                        <div style='text-align: center; background-color: #f5f5f5; padding: 20px; margin: 20px 0; border-radius: 5px;'>
                        <h2 style='font-size: 36px; margin: 0; letter-spacing: 5px;'>{code}</h2>
                        </div>
                        <p>Если вы не можете найти, куда ввести код, пожалуйста, свяжитесь с нами.</p>
                        <p style='margin-top: 40px;'>Команда SwiftTalk</p>
                        </div>
                        </body>
                        </html>"
                };
                mailMessage.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    client.Connect(_smtpHost, _smtpPort, SecureSocketOptions.StartTls);
                    client.Authenticate(_smtpUser, _smtpPass);
                    client.Send(mailMessage);
                    client.Disconnect(true);
                }

                return code;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при отправке email: {ex.Message}\nInner Exception: {ex.InnerException?.Message}", ex);
            }
        }
    }
}