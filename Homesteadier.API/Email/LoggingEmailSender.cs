namespace Homesteadier.API.Email;

/// <summary>
/// Development fallback used when ACS isn't configured. Writes the whole message — including the
/// reset link — to the log, so the full password-reset flow can be exercised from a fresh clone
/// with nothing but Docker: copy the link out of the API console (or the Aspire dashboard) and
/// paste it into the browser.
///
/// Program.cs only selects this in Development; outside it, a missing ACS connection string is a
/// startup failure rather than a silent switch to logging reset links.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Email not sent (no email provider configured). To: {ToAddress}\nSubject: {Subject}\n\n{Body}",
            toAddress,
            subject,
            plainTextBody);

        return Task.CompletedTask;
    }
}
