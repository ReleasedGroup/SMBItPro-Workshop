using Helpdesk.Light.Application.Contracts;

namespace Helpdesk.Light.Application.Abstractions;

public interface IPlatformSettingsService
{
    Task<PlatformSettingsDto> GetAdminSettingsAsync(CancellationToken cancellationToken = default);

    Task<PlatformSettingsDto> UpdateAdminSettingsAsync(PlatformSettingsUpdateRequest request, CancellationToken cancellationToken = default);

    Task<RuntimePlatformSettings> GetRuntimeSettingsAsync(CancellationToken cancellationToken = default);
}
