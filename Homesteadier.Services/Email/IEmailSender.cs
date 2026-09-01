namespace Homesteadier.Services.Email;

/// <summary>
/// Transport for outbound mail. Deliberately knows nothing about templates — composing a
/// specific message is the job of the *Email classes beside this one, so changing copy never
/// touches transport code.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken cancellationToken = default);
}
