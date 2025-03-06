using System.Linq.Expressions;
using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces.Services;

public interface IUserService
{
    Task<User> GetUserByIdAsync(Guid userId);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<IEnumerable<User>> FindUsersAsync(Expression<Func<User, bool>> predicate);
    Task<User> CreateUserAsync(User user, string password);
    Task UpdateUserAsync(User user);
    Task UpdateUserStatusAsync(Guid userId, UserStatus status);
    Task<bool> ValidateCredentialsAsync(string username, string password);
}