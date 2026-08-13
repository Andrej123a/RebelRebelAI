namespace Rebel.Web.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(
            string recipientEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default
        );
    }
}