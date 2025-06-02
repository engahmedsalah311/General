using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IRepository<TEntity> where TEntity : class
    {
        Task<TEntity?> GetByIdAsync(int id);
        Task<IEnumerable<TEntity>> GetAllAsync();
        IQueryable<TEntity> GetAllQueryable();
        Task AddAsync(TEntity entity);
        void Update(TEntity entity);
        void Delete(TEntity entity);
        void DeleteRange(List<TEntity> entities);
        // FirstOrDefault with filter and optional includes
        Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            params Expression<Func<TEntity, object>>[] includes);

        // Search list with filter and includes
        Task<IEnumerable<TEntity>> FindByIncAsync(
            Expression<Func<TEntity, bool>> predicate,
            params Expression<Func<TEntity, object>>[] includes);

        Task<(IEnumerable<TEntity> Data, int TotalCount)> GetPagedAsync(
                           int page,
                           int pageSize,
                           List<Expression<Func<TEntity, bool>>>? filters = null,
                           List<Expression<Func<TEntity, object>>>? includes = null);
    }
}
