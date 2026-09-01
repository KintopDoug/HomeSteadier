namespace Homesteadier.Services.Farms;

/// <summary>
/// Farm invitation token configuration, bound from the "FarmInvitation" config section.
/// </summary>
internal class FarmInvitationSettings
{
    /// <summary>
    /// How long an emailed invitation stays acceptable. Days rather than password-reset's minutes
    /// because accepting an invitation isn't security-sensitive the same way, and the invitee may
    /// not check their email right away.
    /// </summary>
    public int TokenExpiryDays { get; set; } = 7;
}
