using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Interfaces;

namespace ECommerce.Core.Services;

public class AdminCustomerService(IUnitOfWork unitOfWork) : IAdminCustomerService
{
    public Task<PagedResult<CustomerDto>> GetCustomersAsync(CustomerQuery query, CancellationToken cancellationToken = default) =>
        unitOfWork.Users.GetCustomerDtosAsync(query, cancellationToken);
}
