using System.Linq.Expressions;
using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
}