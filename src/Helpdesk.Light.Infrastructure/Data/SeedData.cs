using Helpdesk.Light.Domain.Entities;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Light.Infrastructure.Data;

public static class SeedData
{
    public static async Task EnsureSeededAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        HelpdeskDbContext dbContext = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
        RoleManager<IdentityRole<Guid>> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        string[] roles = [RoleNames.MspAdmin, RoleNames.Technician, RoleNames.EndUser];
        foreach (string role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                IdentityResult roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                EnsureSucceeded(roleResult, $"Failed to create role '{role}'.");
            }
        }

        if (!await dbContext.Customers.AnyAsync(cancellationToken))
        {
            Customer contoso = new(SeedDataConstants.ContosoCustomerId, "Contoso", true);
            contoso.AddDomain(Guid.Parse("f0b0dba7-94a0-4263-8615-2af76356d6ca"), "contoso.com", true);

            Customer fabrikam = new(SeedDataConstants.FabrikamCustomerId, "Fabrikam", true);
            fabrikam.AddDomain(Guid.Parse("8df89d00-0c14-4ec8-93f9-2347bbf75dc2"), "fabrikam.com", true);

            dbContext.Customers.AddRange(contoso, fabrikam);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureUserAsync(userManager, SeedDataConstants.AdminEmail, "MSP Admin", null, RoleNames.MspAdmin, cancellationToken);
        await EnsureUserAsync(userManager, SeedDataConstants.ContosoTechEmail, "Contoso Tech", SeedDataConstants.ContosoCustomerId, RoleNames.Technician, cancellationToken);
        await EnsureUserAsync(userManager, SeedDataConstants.FabrikamTechEmail, "Fabrikam Tech", SeedDataConstants.FabrikamCustomerId, RoleNames.Technician, cancellationToken);
        await EnsureUserAsync(userManager, SeedDataConstants.ContosoEndUserEmail, "Contoso User", SeedDataConstants.ContosoCustomerId, RoleNames.EndUser, cancellationToken);
        await EnsureUserAsync(userManager, SeedDataConstants.FabrikamEndUserEmail, "Fabrikam User", SeedDataConstants.FabrikamCustomerId, RoleNames.EndUser, cancellationToken);

        if (!await dbContext.KnowledgeArticles.AnyAsync(cancellationToken))
        {
            dbContext.KnowledgeArticles.AddRange(
                new Domain.Ai.KnowledgeArticle(
                    Guid.Parse("01af40de-97f7-44a8-848e-84186f6f2f5d"),
                    null,
                    null,
                    "Password Reset Basics",
                    "Validate identity before resetting credentials. Enforce MFA reset checks.",
                    aiGenerated: false,
                    editedByUserId: null,
                    DateTime.UtcNow),
                new Domain.Ai.KnowledgeArticle(
                    Guid.Parse("3d22ec72-c57e-4fbc-8135-7d8fd6b4be45"),
                    null,
                    null,
                    "Service Outage Triage",
                    "Confirm scope, impacted services, and rollback path. Provide 30 minute status updates.",
                    aiGenerated: false,
                    editedByUserId: null,
                    DateTime.UtcNow));

            foreach (Domain.Ai.KnowledgeArticle article in dbContext.KnowledgeArticles.Local.ToList())
            {
                article.Publish(null, DateTime.UtcNow);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string displayName,
        Guid? customerId,
        string role,
        CancellationToken cancellationToken)
    {
        ApplicationUser? existing = await userManager.Users.SingleOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (existing is null)
        {
            ApplicationUser created = new()
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CustomerId = customerId,
                DisplayName = displayName
            };

            IdentityResult createResult = await userManager.CreateAsync(created, SeedDataConstants.DefaultPassword);
            EnsureSucceeded(createResult, $"Failed to create user '{email}'.");

            IdentityResult addRoleResult = await userManager.AddToRoleAsync(created, role);
            EnsureSucceeded(addRoleResult, $"Failed to assign role '{role}' to '{email}'.");
            return;
        }

        bool requiresUpdate =
            existing.CustomerId != customerId ||
            !string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal) ||
            !string.Equals(existing.UserName, email, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase) ||
            !existing.EmailConfirmed;

        if (requiresUpdate)
        {
            existing.CustomerId = customerId;
            existing.DisplayName = displayName;
            existing.UserName = email;
            existing.Email = email;
            existing.EmailConfirmed = true;

            IdentityResult updateResult = await userManager.UpdateAsync(existing);
            EnsureSucceeded(updateResult, $"Failed to update user '{email}'.");
        }

        if (!await userManager.IsInRoleAsync(existing, role))
        {
            IList<string> currentRoles = await userManager.GetRolesAsync(existing);
            if (currentRoles.Count > 0)
            {
                IdentityResult removeRoleResult = await userManager.RemoveFromRolesAsync(existing, currentRoles);
                EnsureSucceeded(removeRoleResult, $"Failed to clear roles for '{email}'.");
            }

            IdentityResult addRoleResult = await userManager.AddToRoleAsync(existing, role);
            EnsureSucceeded(addRoleResult, $"Failed to assign role '{role}' to '{email}'.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        string errors = string.Join("; ", result.Errors.Select(item => item.Description));
        throw new InvalidOperationException($"{message} Errors: {errors}");
    }
}
