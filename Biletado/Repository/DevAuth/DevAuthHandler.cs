using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Biletado.Repository.DevAuth;

#pragma warning disable CS0618 // ISystemClock is obsolete; acceptable for a dev fallback authentication handler
public class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Create a simple development principal so [Authorize] works without real tokens
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "dev"),
            new Claim(ClaimTypes.Name, "dev")
        };
        var identity = new ClaimsIdentity(claims, "DevAuth");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#pragma warning restore CS0618
