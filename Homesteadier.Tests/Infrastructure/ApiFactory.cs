using Homesteadier.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Homesteadier.Tests.Infrastructure;

/// <summary>The real API, hosted in-process. The only substitution is the outbound email transport.</summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            // Nothing is actually sent; mail is recorded so a test can read the invitation link a person
            // would have received.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}
