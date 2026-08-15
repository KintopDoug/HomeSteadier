namespace Homesteadier.API.Email;

/// <summary>
/// Outbound mail configuration. SenderAddress/SenderDisplayName are bound from the "Email"
/// config section; ConnectionString is a secret sourced from the ACS_CONNECTION_STRING
/// environment variable, like JWT_SIGNING_KEY.
/// </summary>
public class EmailSettings
{
    /// <summary>
    /// Azure Communication Services connection string. Null when ACS isn't configured, which is
    /// the normal state locally — see the sender selection in Program.cs.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Must be an address on a domain verified in the ACS Email resource; ACS rejects anything
    /// else outright.
    /// </summary>
    public string SenderAddress { get; set; } = string.Empty;

    public string SenderDisplayName { get; set; } = "HomeSteadier";
}
