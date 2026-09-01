using WeatherUserActions.Email;

namespace WeatherUserActions.Tests.Fakes
{
    // Records every message instead of sending it, so tests can assert what the analytics
    // worker tried to email (including attachments).
    public class FakeEmailSender : IEmailSender
    {
        public record SentEmail(string ToEmail, string Subject, string Body, IReadOnlyList<EmailAttachment> Attachments);

        private readonly bool _throws;
        private int _transientFailures;

        // throws: fail every send. transientFailures: fail the first N sends, then succeed - used to
        // exercise the worker's delivery-retry path.
        public FakeEmailSender(bool throws = false, int transientFailures = 0)
        {
            _throws = throws;
            _transientFailures = transientFailures;
        }

        public List<SentEmail> Sent { get; } = [];

        public Task SendAsync(
            string toEmail,
            string subject,
            string body,
            IReadOnlyCollection<EmailAttachment>? attachments = null,
            CancellationToken cancellationToken = default)
        {
            if (_throws)
            {
                throw new InvalidOperationException("Fake email sender configured to fail.");
            }

            if (_transientFailures > 0)
            {
                _transientFailures--;
                throw new InvalidOperationException("Fake email sender: transient failure.");
            }

            Sent.Add(new SentEmail(toEmail, subject, body, attachments?.ToList() ?? []));
            return Task.CompletedTask;
        }
    }
}
