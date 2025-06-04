using Application.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly AppDbContext _context;
        private readonly DbSet<T> _dbSet;
        private readonly IMapper _mapper;

        public Repository(AppDbContext context, IMapper mapper)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _dbSet = context.Set<T>();
        }
        public async Task<IEnumerable<T>> GetAllAsList() => await _dbSet.AsNoTracking().ToListAsync();
        public IQueryable<T> GetAllAsQueryable() => _dbSet.AsNoTracking().AsQueryable();
        public async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);
        public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);
        public void Update(T entity) => _dbSet.Update(entity);
        public void Delete(T entity) => _dbSet.Remove(entity);
        public void DeleteRange(List<T> entities) => _dbSet.RemoveRange(entities);

        public async Task<(IEnumerable<TDto> Data, int TotalCount)> GetPagedMappedAsync<TDto>(
                           int page,
                           int pageSize,
                           List<Expression<Func<T, bool>>>? filters = null,
                           List<Expression<Func<T, object>>>? includes = null)
        {
            IQueryable<T> query = _dbSet.AsQueryable();

            // Apply filters
            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    query = query.Where(filter);
                }
            }

            //apply includes
            if (includes != null)
            {
                foreach (var include in includes)
                    query = query.AsNoTracking().Include(include);
            }


            var totalCount = await query.CountAsync();

            var data =await query.ProjectTo<TDto>(_mapper.ConfigurationProvider)
                .Skip((page - 1) * pageSize)
                .Take(pageSize).ToListAsync();

            return (data, totalCount);
        }

        public async Task<(IEnumerable<T> Data, int TotalCount)> GetPagedAsync<TDto>(
                           int page,
                           int pageSize,
                           List<Expression<Func<T, bool>>>? filters = null,
                           List<Expression<Func<T, object>>>? includes = null)
        {
            IQueryable<T> query = _dbSet.AsNoTracking().AsQueryable();

            // Apply filters
            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    query = query.Where(filter);
                }
            }

            //apply includes
            if (includes != null)
            {
                foreach (var include in includes)
                    query = query.Include(include);
            }


            var totalCount = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize).ToListAsync();

            return (data, totalCount);
        }

        public async Task<IEnumerable<TDto>> FindByIncMappedAsync<TDto>(
                           List<Expression<Func<T, bool>>>? filters = null,
                           List<Expression<Func<T, object>>>? includes = null)
        {
            IQueryable<T> query = _dbSet.AsNoTracking().AsQueryable();

            // Apply filters
            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    query = query.Where(filter);
                }
            }

            //apply includes
            if (includes != null)
            {
                foreach (var include in includes)
                    query = query.Include(include);
            }

            var data = await query.ProjectTo<TDto>(_mapper.ConfigurationProvider).ToListAsync();

            return (data);
        }


        public async Task<IEnumerable<T>> FindByIncAsync<TDto>(
                           List<Expression<Func<T, bool>>>? filters = null,
                           List<Expression<Func<T, object>>>? includes = null)
        {
            IQueryable<T> query = _dbSet.AsQueryable();

            // Apply filters
            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    query = query.Where(filter);
                }
            }

            //apply includes
            if (includes != null)
            {
                foreach (var include in includes)
                    query = query.Include(include);
            }

            var data = await query.ToListAsync();

            return (data);
        }

        public async Task<T?> FirstOrDefaultIncAsync<TDto>(
                           List<Expression<Func<T, bool>>>? filters = null,
                           List<Expression<Func<T, object>>>? includes = null)
        {
            IQueryable<T> query = _dbSet.AsQueryable();

            // Apply filters
            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    query = query.Where(filter);
                }
            }

            //apply includes
            if (includes != null)
            {
                foreach (var include in includes)
                    query = query.Include(include);
            }

            var data = await query.FirstOrDefaultAsync();

            return (data);
        }



    }
}
