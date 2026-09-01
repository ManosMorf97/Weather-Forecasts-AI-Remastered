using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace WeatherUserActions.Email
{
    // MailKit-backed transport. Configured via the "Email" section:
    //   Host, Port (default 587), From, UserName, Password, UseStartTls (default true).
    public class SmtpEmailSender : IEmailSender
    {
        private readonly ILogger<SmtpEmailSender> _logger;
        private readonly string _host;
        private readonly int _port;
        private readonly string _from;
        private readonly string? _userName;
        private readonly string? _password;
        private readonly bool _useStartTls;

        public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
        {
            _logger = logger;

            var email = configuration.GetSection("Email");
            _host = email["Host"] ?? throw new InvalidOperationException("Email:Host is not configured.");
            _port = email.GetValue<int?>("Port") ?? 587;
            _from = email["From"] ?? throw new InvalidOperationException("Email:From is not configured.");
            _userName = email["UserName"];
            _password = email["Password"];
            _useStartTls = email.GetValue<bool?>("UseStartTls") ?? true;
        }

        public async Task SendAsync(
            string toEmail,
            string subject,
            string body,
            IReadOnlyCollection<EmailAttachment>? attachments = null,
            CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_from));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder { TextBody = body };
            foreach (var attachment in attachments ?? [])
            {
                builder.Attachments.Add(
                    attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            var socketOptions = _useStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_host, _port, socketOptions, cancellationToken);

            if (!string.IsNullOrEmpty(_userName))
            {
                await client.AuthenticateAsync(_userName, _password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            _logger.LogInformation("Sent email to {ToEmail} | {Subject}", toEmail, subject);
        }
    }
}
