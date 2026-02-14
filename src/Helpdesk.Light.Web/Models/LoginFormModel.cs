using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Light.Web.Models;

public sealed class LoginFormModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
