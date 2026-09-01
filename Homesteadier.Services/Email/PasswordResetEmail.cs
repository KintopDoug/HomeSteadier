using System.Net;

namespace Homesteadier.Services.Email;

/// <summary>
/// Composes the password-reset message. Kept apart from <see cref="IEmailSender"/> so copy
/// changes don't touch transport code, and out of the controller so the action stays readable.
/// </summary>
public static class PasswordResetEmail
{
    public static (string Subject, string HtmlBody, string PlainTextBody) Compose(
        string firstName,
        string resetLink,
        int expiryMinutes)
    {
        const string Subject = "Reset your HomeSteadier password";

        // The link is built from a config-supplied base URL and a generated token, but it lands
        // in an href either way — encode it rather than trusting that.
        var encodedLink = WebUtility.HtmlEncode(resetLink);
        var encodedName = WebUtility.HtmlEncode(firstName);

        var html = $"""
            <p>Hi {encodedName},</p>
            <p>We received a request to reset your HomeSteadier password. Choose a new one here:</p>
            <p><a href="{encodedLink}">Reset my password</a></p>
            <p>This link expires in {expiryMinutes} minutes and can only be used once. If you asked
            for more than one reset email, only the most recent link will work.</p>
            <p>If you didn't request this, you can ignore this email — your password won't change.</p>
            <p>— HomeSteadier</p>
            """;

        var plainText = $"""
            Hi {firstName},

            We received a request to reset your HomeSteadier password. Choose a new one here:

            {resetLink}

            This link expires in {expiryMinutes} minutes and can only be used once. If you asked for
            more than one reset email, only the most recent link will work.

            If you didn't request this, you can ignore this email — your password won't change.

            — HomeSteadier
            """;

        return (Subject, html, plainText);
    }
}
