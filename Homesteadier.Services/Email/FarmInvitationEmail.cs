using System.Net;

namespace Homesteadier.Services.Email;

/// <summary>
/// Composes farm invitation messages. Kept apart from <see cref="IEmailSender"/> so copy changes
/// don't touch transport code, and out of the controller so the action stays readable.
/// </summary>
internal static class FarmInvitationEmail
{
    public static (string Subject, string HtmlBody, string PlainTextBody) ComposeForExistingUser(
        string farmName,
        string roleName,
        string acceptLink,
        int expiryDays)
    {
        var subject = $"You've been invited to join {farmName} on HomeSteadier";

        var encodedLink = WebUtility.HtmlEncode(acceptLink);
        var encodedFarmName = WebUtility.HtmlEncode(farmName);
        var encodedRoleName = WebUtility.HtmlEncode(roleName);

        var html = $"""
            <p>You've been invited to join <strong>{encodedFarmName}</strong> on HomeSteadier as
            <strong>{encodedRoleName}</strong>.</p>
            <p><a href="{encodedLink}">Accept the invitation</a></p>
            <p>This link expires in {expiryDays} days. If you weren't expecting this, you can ignore
            this email.</p>
            <p>— HomeSteadier</p>
            """;

        var plainText = $"""
            You've been invited to join {farmName} on HomeSteadier as {roleName}.

            Accept the invitation here:

            {acceptLink}

            This link expires in {expiryDays} days. If you weren't expecting this, you can ignore
            this email.

            — HomeSteadier
            """;

        return (subject, html, plainText);
    }

    public static (string Subject, string HtmlBody, string PlainTextBody) ComposeForNewUser(
        string farmName,
        string roleName,
        string registerLink,
        int expiryDays)
    {
        var subject = $"You've been invited to join {farmName} on HomeSteadier";

        var encodedLink = WebUtility.HtmlEncode(registerLink);
        var encodedFarmName = WebUtility.HtmlEncode(farmName);
        var encodedRoleName = WebUtility.HtmlEncode(roleName);

        var html = $"""
            <p>You've been invited to join <strong>{encodedFarmName}</strong> on HomeSteadier as
            <strong>{encodedRoleName}</strong>.</p>
            <p>Create your account to get started:</p>
            <p><a href="{encodedLink}">Create your account</a></p>
            <p>This link expires in {expiryDays} days. If you weren't expecting this, you can ignore
            this email.</p>
            <p>— HomeSteadier</p>
            """;

        var plainText = $"""
            You've been invited to join {farmName} on HomeSteadier as {roleName}.

            Create your account to get started:

            {registerLink}

            This link expires in {expiryDays} days. If you weren't expecting this, you can ignore
            this email.

            — HomeSteadier
            """;

        return (subject, html, plainText);
    }
}
