using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Rebel.Web.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly IWebHostEnvironment _environment;

        public EmailService(
            IOptions<EmailSettings> options,
            IWebHostEnvironment environment)
        {
            _settings = options.Value;
            _environment = environment;
        }

        public async Task SendEmailAsync(
            string recipientEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                throw new ArgumentException(
                    "Recipient email is required.",
                    nameof(recipientEmail)
                );
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new ArgumentException(
                    "Email subject is required.",
                    nameof(subject)
                );
            }

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    _settings.SenderName,
                    _settings.SenderEmail
                )
            );

            message.To.Add(
                MailboxAddress.Parse(recipientEmail)
            );

            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };

            var logoPath = Path.Combine(
                _environment.WebRootPath,
                "images",
                "email",
                "rebel-logo.png"
            );

            if (File.Exists(logoPath))
            {
                var logo = bodyBuilder.LinkedResources.Add(logoPath);

                logo.ContentId = "rebel-logo";
                logo.ContentDisposition = new ContentDisposition(
                    ContentDisposition.Inline
                );
            }

            message.Body = bodyBuilder.ToMessageBody();

            using var smtpClient = new SmtpClient();

            await smtpClient.ConnectAsync(
                _settings.SmtpServer,
                _settings.Port,
                SecureSocketOptions.Auto,
                cancellationToken
            );

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                await smtpClient.AuthenticateAsync(
                    _settings.Username,
                    _settings.Password,
                    cancellationToken
                );
            }

            await smtpClient.SendAsync(
                message,
                cancellationToken
            );

            await smtpClient.DisconnectAsync(
                true,
                cancellationToken
            );
        }
    }
}