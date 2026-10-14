using ECommerce.Core.Entities;

namespace ECommerce.Core.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}
