namespace ECommerce.Core.Interfaces;

// The logged-in user, read from the validated JWT. Services use this instead of a user ID
// from the route or body, so a customer can't act as someone else by changing a number.
public interface ICurrentUserService
{
    int UserId { get; }
}
