using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.DTOs.Common;

namespace ECommerce.Core.Interfaces;

// Admin-only and read-only.
public interface IAdminCustomerService
{
    Task<PagedResult<CustomerDto>> GetCustomersAsync(CustomerQuery query, CancellationToken cancellationToken = default);
}
