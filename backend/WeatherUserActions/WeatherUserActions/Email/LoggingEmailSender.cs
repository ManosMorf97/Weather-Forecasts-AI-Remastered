namespace WeatherUserActions.Email
{
    // Fallback transport: writes the message to the log instead of sending it. Registered when no
    // SMTP host is configured, so the analytics pipeline stays runnable without a mail server.
    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(
            string toEmail,
            string subject,
            string body,
            IReadOnlyCollection<EmailAttachment>? attachments = null,
            CancellationToken cancellationToken = default)
        {
            var attachmentSummary = attachments is { Count: > 0 }
                ? " + " + string.Join(", ", attachments.Select(a => $"{a.FileName} ({a.Content.Length} bytes)"))
                : string.Empty;

            _logger.LogInformation("Email to {ToEmail} | {Subject}{Attachments}\n{Body}", toEmail, subject, attachmentSummary, body);
            return Task.CompletedTask;
        }
    }
}
