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
        Task<IEnumerable<TEntity>> GetAllAsList();
        IQueryable<TEntity> GetAllAsQueryable();
        Task AddAsync(TEntity entity);
        void Update(TEntity entity);
        void Delete(TEntity entity);
        void DeleteRange(List<TEntity> entities);
        // FirstOrDefault with filter and optional includes
        Task<TEntity?> FirstOrDefaultIncAsync<TDto>(
                            List<Expression<Func<TEntity, bool>>>? filters = null,
                            List<Expression<Func<TEntity, object>>>? includes = null);

        // Search list with filter and includes
        Task<IEnumerable<TDto>> FindByIncMappedAsync<TDto>(
                           List<Expression<Func<TEntity, bool>>>? filters = null,
                           List<Expression<Func<TEntity, object>>>? includes = null);

        Task<(IEnumerable<TDto> Data, int TotalCount)> GetPagedMappedAsync<TDto>(
                           int page,
                           int pageSize,
                           List<Expression<Func<TEntity, bool>>>? filters = null,
                           List<Expression<Func<TEntity, object>>>? includes = null);

        Task<(IEnumerable<TEntity> Data, int TotalCount)> GetPagedAsync<TDto>(
                           int page,
                           int pageSize,
                           List<Expression<Func<TEntity, bool>>>? filters = null,
                           List<Expression<Func<TEntity, object>>>? includes = null);

        Task<IEnumerable<TEntity>> FindByIncAsync<TDto>(
                           List<Expression<Func<TEntity, bool>>>? filters = null,
                           List<Expression<Func<TEntity, object>>>? includes = null);

        Task<IEnumerable<TEntity>> SearchAsync<TSearchDto>(TSearchDto searchDto, List<Expression<Func<TEntity, object>>>? includes = null);

        Task<(IEnumerable<TDto> Data, int TotalCount)> SearchPagedMappedAsync<TDto, TSearchDto>(
                           TSearchDto searchDto,
                           int page,
                           int pageSize,
                           List<Expression<Func<TEntity, object>>>? includes = null);

        Task<(IEnumerable<TEntity> Data, int TotalCount)> SearchPagedAsync<TSearchDto>(
                           TSearchDto searchDto,
                           int page,
                           int pageSize,
                           List<Expression<Func<TEntity, object>>>? includes = null);
    }
}
