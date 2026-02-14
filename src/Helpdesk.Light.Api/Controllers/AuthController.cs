using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenIssuer jwtTokenIssuer) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        ApplicationUser? user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized();
        }

        bool validPassword = await userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
        {
            return Unauthorized();
        }

        IList<string> roles = await userManager.GetRolesAsync(user);
        string role = roles.FirstOrDefault() ?? string.Empty;
        string token = jwtTokenIssuer.IssueToken(user.Id, user.Email ?? string.Empty, role, user.CustomerId, out DateTime expiresUtc);

        return Ok(new LoginResponse(token, expiresUtc, user.Id, user.Email ?? string.Empty, role, user.CustomerId));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    public ActionResult<MeResponse> Me([FromServices] ITenantContextAccessor tenantContextAccessor)
    {
        TenantAccessContext context = tenantContextAccessor.Current;

        return Ok(new MeResponse(
            context.UserId ?? Guid.Empty,
            context.Email,
            context.Role,
            context.CustomerId));
    }
}
