using ECommerce.Core.DTOs.Admin;

namespace ECommerce.Core.Interfaces;

public interface IAdminDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default);
}
