using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Homesteadier.Services.Email;

namespace Homesteadier.Tests.Infrastructure;

public sealed record SentEmail(string To, string Subject, string Html, string Text);

/// <summary>Records outbound mail instead of sending it.</summary>
public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(new SentEmail(toAddress, subject, htmlBody, plainTextBody));
        return Task.CompletedTask;
    }

    /// <summary>The raw token from the newest invitation link sent to <paramref name="address"/>.</summary>
    public string InvitationTokenFor(string address)
    {
        var email = _sent.Reverse().FirstOrDefault(e =>
            string.Equals(e.To, address, StringComparison.OrdinalIgnoreCase) && TokenInLink().IsMatch(e.Text))
            ?? throw new InvalidOperationException($"No invitation email with a link was sent to {address}.");

        return Uri.UnescapeDataString(TokenInLink().Match(email.Text).Groups["token"].Value);
    }

    /// <summary>The raw token from the newest password-reset link sent to <paramref name="address"/>.</summary>
    public string PasswordResetTokenFor(string address)
    {
        var email = _sent.Reverse().FirstOrDefault(e =>
            string.Equals(e.To, address, StringComparison.OrdinalIgnoreCase)
            && e.Text.Contains("reset-password", StringComparison.Ordinal)
            && TokenInLink().IsMatch(e.Text))
            ?? throw new InvalidOperationException($"No password reset email with a link was sent to {address}.");

        return Uri.UnescapeDataString(TokenInLink().Match(email.Text).Groups["token"].Value);
    }

    /// <summary>How many password-reset emails (any link) have gone to <paramref name="address"/>.</summary>
    public int PasswordResetCountFor(string address) =>
        _sent.Count(e => string.Equals(e.To, address, StringComparison.OrdinalIgnoreCase)
            && e.Text.Contains("reset-password", StringComparison.Ordinal));

    [GeneratedRegex(@"[?&](?:invite)?[tT]oken=(?<token>[^\s&""<>]+)")]
    private static partial Regex TokenInLink();
}
