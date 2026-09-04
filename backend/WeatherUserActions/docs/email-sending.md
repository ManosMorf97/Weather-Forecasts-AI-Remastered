# Email Sending — Learning Notes

Notes for understanding how the app sends email (the analytics report delivery,
and any future notification email).

Files involved:

| File | Role |
|------|------|
| `Email/IEmailSender.cs` | The contract: "send a message to someone" |
| `Email/SmtpEmailSender.cs` | Real delivery over SMTP (MailKit) |
| `Email/LoggingEmailSender.cs` | Fake delivery: writes the message to the log |
| `Program.cs` | Picks which one to use, based on config |
| `Services/AnalyticsService.cs` | A caller: builds the message, calls `SendAsync` |

---

## 1. How email actually travels

You don't "send an email" directly to a person. You hand it to a **mail server**
(an SMTP server) and *it* passes it along until it reaches the recipient's inbox.

```
our app ──SMTP──► our sending mail server ──► ... internet ... ──► recipient's mail server ──► inbox
```

- **SMTP** = Simple Mail Transfer Protocol. The language mail servers speak to
  accept and relay a message. Port **587** is the modern standard for a program
  submitting a new message (port 25 is server-to-server, port 465 is older
  "implicit TLS").
- To use a sending mail server you must **log in** to it, exactly like logging
  into a mailbox — with a username and password. That login is *the app's own
  mail account*, not a user of our app.

Think of it like posting a physical letter:

- You write the letter (the `MimeMessage`).
- You drop it at the post office (`ConnectAsync` + `SendAsync` to the SMTP server).
- The postal network delivers it (nothing we control).
- The post office needs to know it's really you with an account (`AuthenticateAsync`).

---

## 2. The pieces of a message

A single email is assembled from:

| Part | In our code | Example |
|------|-------------|---------|
| From | `_from` (config) | `Weather Analytics <noreply@weatheranalytics.com>` |
| To | `toEmail` (parameter) | the user's address, looked up from Appwrite |
| Subject | `subject` (parameter) | "Your analytics report is ready" |
| Body | `body` (parameter) | plain text |
| Attachments | `attachments` (parameter) | `analytics-<batchId>.pdf` |

**From** is fixed — it's always our app's address, read from config once.
**To / Subject / Body / Attachments** change on every send and are passed in by
the caller (`AnalyticsService`).

---

## 3. The two implementations

Both implement the same interface:

```csharp
public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string body,
        IReadOnlyCollection<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default);
}
```

The rest of the app only knows `IEmailSender`. It never cares which one is wired
in — that's the point of the interface.

### `LoggingEmailSender` — the development default

Does **not** send anything. It writes "here's the email I would have sent" to the
log. This lets you run and test the whole app locally without a mail account and
without spamming real inboxes.

### `SmtpEmailSender` — real delivery

Uses **MailKit** (the `SmtpClient` / `MimeMessage` types) to actually connect to
an SMTP server and submit the message.

### `Program.cs` chooses at startup

```csharp
if (!string.IsNullOrWhiteSpace(builder.Configuration["Email:Host"]))
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();   // host set -> real
else
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>(); // no host -> fake
```

Rule: **no `Email:Host` in config → fake sender.** Set a host → real sender.

---

## 4. Configuration (`Email` section)

`appsettings.Development.json` ships with the section present but **empty** — the
same as the `Database` and `Appwrite` sections. Real values are secrets and are
never committed. You supply them via **user secrets** or **environment
variables**.

| Key | Meaning | Default |
|-----|---------|---------|
| `Email:Host` | SMTP server hostname | *(none — required for real send)* |
| `Email:Port` | SMTP port | `587` |
| `Email:From` | address shown in the "From:" line | *(none — required for real send)* |
| `Email:UserName` | login for the SMTP server | *(optional)* |
| `Email:Password` | password / app-token for the SMTP server | *(optional)* |
| `Email:UseStartTls` | upgrade the connection to TLS after connecting | `true` |

`UserName` / `Password` are optional because some internal/test SMTP servers
accept mail without a login. Any real provider (Gmail, SendGrid, …) requires them.

### Setting values with user secrets

```bash
cd backend/WeatherUserActions/WeatherUserActions
dotnet user-secrets set "Email:Host" "smtp.gmail.com"
dotnet user-secrets set "Email:From" "you@gmail.com"
dotnet user-secrets set "Email:UserName" "you@gmail.com"
dotnet user-secrets set "Email:Password" "<16-char app password>"
```

### Setting values with environment variables

Nested keys use a double underscore:

```
Email__Host=smtp.gmail.com
Email__From=you@gmail.com
Email__UserName=you@gmail.com
Email__Password=...
```

### Using a real provider

- **Gmail**: host `smtp.gmail.com`, port `587`. You cannot use your normal
  password — enable 2-factor auth, then create an **App Password** and use that
  as `Email:Password`.
- **SendGrid / Mailgun / Amazon SES / etc.**: they give you an SMTP host, a
  username, and an API key to use as the password. Same six settings. Here
  `Email:From` is your **own** verified domain address — e.g.
  `noreply@weatheranalytics.com` — while `Email:UserName` / `Email:Password` are
  the provider's credentials. The provider relays mail *as* your domain once you
  verify it (SPF/DKIM DNS records).

Gmail is fine for local testing; a dedicated provider with the
`weatheranalytics.com` domain is what you'd use for real delivery, so the "From"
line reads `noreply@weatheranalytics.com` instead of a personal address.

---

## 5. What `SmtpEmailSender` does

### Constructor — read config once

`SmtpEmailSender` is registered as a **singleton**, so its constructor runs
exactly once (the first time something needs an `IEmailSender`). It copies the
config values into fields and validates the required ones:

```csharp
_host = email["Host"] ?? throw new InvalidOperationException("Email:Host is not configured.");
_port = email.GetValue<int?>("Port") ?? 587;
_from = email["From"] ?? throw new InvalidOperationException("Email:From is not configured.");
_userName = email["UserName"];
_password = email["Password"];
_useStartTls = email.GetValue<bool?>("UseStartTls") ?? true;
```

If `Host` or `From` is missing it throws **at startup** — a loud, early failure
rather than a silent broken send later.

### `SendAsync` — build and submit one message

Runs on every send:

```csharp
var message = new MimeMessage();
message.From.Add(MailboxAddress.Parse(_from));      // our app's address
message.To.Add(MailboxAddress.Parse(toEmail));      // the recipient
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
await client.ConnectAsync(_host, _port, socketOptions, cancellationToken);   // open connection

if (!string.IsNullOrEmpty(_userName))
    await client.AuthenticateAsync(_userName, _password ?? string.Empty, cancellationToken);   // log in

await client.SendAsync(message, cancellationToken);          // submit
await client.DisconnectAsync(quit: true, cancellationToken); // close politely
```

Step by step:

1. **Build** the `MimeMessage` — from, to, subject, text body, attachments.
   `EmailAttachment` is our small record: file name, MIME content-type
   (`application/pdf`), and the raw `byte[]` (the PDF straight from QuestPDF —
   never written to disk).
2. **Connect** to the SMTP server. `StartTls` means: connect in plain text, then
   immediately upgrade the socket to encrypted before sending anything sensitive.
3. **Authenticate** if a username is configured — this is the app's mail-account
   login.
4. **Send** the message.
5. **Disconnect** with `quit: true` so the server knows we're done cleanly.

A new `SmtpClient` connection is opened and closed **per send**. Simple and
correct for our low volume. A high-volume system would pool/reuse the connection.

---

## 6. How a send is triggered (analytics flow)

`SmtpEmailSender` is never called directly by a controller. The chain:

```
AnalyticsReportWorker tick (every ~5s)
  -> AnalyticsService.ProcessQueuedReportsAsync
     -> DeliverCompletedBatchesAsync
        -> DeliverBatchAsync:
             email = await _appwriteUsersService.GetEmailAsync(batch.UserId)   // recipient
             pdf   = _reportRenderer.RenderPdf(batch.BatchId, sections)        // attachment bytes
             await _emailSender.SendAsync(email, subject, body, [pdf attachment])
             -> if it succeeds: mark the batch Delivered
```

Notice the recipient address is **not** in config — it's fetched at send time
from Appwrite using the stored `UserId`. There is no logged-in user / JWT in a
background job, so the lookup uses the Appwrite API key.

---

## 7. Failure handling

Sending email is the least reliable step (network, remote server, auth). The
design keeps failures cheap:

| Failure | What happens |
|---------|--------------|
| Email lookup (Appwrite) fails | Batch stays undelivered, retried next tick |
| PDF render fails | Falls back to a **text-only** email — the numbers still go out |
| SMTP connect / auth / send fails | `SendAsync` throws; batch stays undelivered; retried next tick |
| Send succeeds | Batch marked `Delivered` — never sent again |

Because generation (pass 1) and delivery (pass 2) are separate, a mail outage
never causes the statistics to be recomputed. See
`background-and-report-generation.md` for that split.

`SendSafelyAsync` in `AnalyticsService` wraps the call in a try/catch and returns
a `bool` so the worker can decide whether to mark the batch delivered.

---

## 8. Testing

- Unit tests use `Tests/Fakes/FakeEmailSender.cs` — a fake `IEmailSender` that
  records what it was asked to send (recipient, subject, attachment names) — no
  real SMTP.
- `LoggingEmailSender` is the manual equivalent when running the app: trigger a
  report, read the log, confirm the "to", subject, and attachment look right.
- To test true end-to-end delivery, point the `Email` settings at a real test
  mailbox (a spare Gmail with an app password, or a service like Mailtrap that
  captures mail without delivering it).

---

## 9. Glossary

| Term | Meaning |
|------|---------|
| SMTP | Protocol for submitting/relaying email between mail servers |
| SMTP server / host | The mail server we hand outbound messages to (e.g. `smtp.gmail.com`) |
| Port 587 | Standard port for a program submitting a new message |
| STARTTLS | Connect in plain text, then upgrade the same connection to encrypted |
| MailKit / MimeKit | The .NET libraries we use for SMTP (`SmtpClient`) and message building (`MimeMessage`) |
| `MimeMessage` | An in-memory email: from, to, subject, body, attachments |
| App Password | A provider-generated password for programs, used instead of your real password |
| From address | Our app's own sending address — fixed, from config |
| To address | The recipient — passed in per send, looked up from Appwrite |
| Singleton | One instance for the app's lifetime; config is read once in its constructor |
| Fake / logging sender | `LoggingEmailSender` — logs instead of sending, used in dev |
