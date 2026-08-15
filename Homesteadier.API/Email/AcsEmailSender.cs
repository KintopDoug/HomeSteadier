using Azure;
using Azure.Communication.Email;

namespace Homesteadier.API.Email;

/// <summary>
/// Sends through Azure Communication Services. The ACS resource is provisioned out of band —
/// Aspire has no hosting integration for it — so all this needs is the connection string, which
/// AppHost injects into the container as ACS_CONNECTION_STRING.
/// </summary>
public class AcsEmailSender : IEmailSender
{
    private readonly EmailClient _client;
    private readonly EmailSettings _settings;
    private readonly ILogger<AcsEmailSender> _logger;

    public AcsEmailSender(EmailClient client, EmailSettings settings, ILogger<AcsEmailSender> logger)
    {
        _client = client;
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage(
            senderAddress: _settings.SenderAddress,
            content: new EmailContent(subject)
            {
                Html = htmlBody,
                PlainText = plainTextBody,
            },
            recipients: new EmailRecipients([new EmailAddress(toAddress, _settings.SenderDisplayName)]));

        try
        {
            // WaitUntil.Started, not Completed: polling until ACS reports delivery would hold the
            // HTTP request open for seconds against a pipeline we can't influence, and the caller
            // (forgot-password) returns the same 202 either way by design.
            var operation = await _client.SendAsync(WaitUntil.Started, message, cancellationToken);
            _logger.LogInformation("Queued email with Azure Communication Services, operation {OperationId}.", operation.Id);
        }
        catch (RequestFailedException ex)
        {
            // Swallowed on purpose. The only caller must not vary its response based on whether
            // mail went out — that would turn delivery failures into an account-existence oracle.
            _logger.LogError(ex, "Azure Communication Services rejected an outbound email.");
        }
    }
}
