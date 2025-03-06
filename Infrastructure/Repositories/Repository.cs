using System.Linq.Expressions;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _entities;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _entities = context.Set<T>();
    }

    public virtual async Task<T> GetByIdAsync(Guid id)
    {
        return await _entities.FindAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _entities.ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _entities.Where(predicate).ToListAsync();
    }

    public virtual async Task AddAsync(T entity)
    {
        await _entities.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public virtual async Task UpdateAsync(T entity)
    {
        _context.Entry(entity).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _entities.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}