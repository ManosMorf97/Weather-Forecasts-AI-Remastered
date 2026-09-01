namespace WeatherUserActions.Email
{
    // Sends a plain-text email, optionally with file attachments (the analytics chart PDF).
    public interface IEmailSender
    {
        Task SendAsync(
            string toEmail,
            string subject,
            string body,
            IReadOnlyCollection<EmailAttachment>? attachments = null,
            CancellationToken cancellationToken = default);
    }
}
