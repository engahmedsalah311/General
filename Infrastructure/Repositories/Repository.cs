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
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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





        #region advanced search
        public async Task<IEnumerable<T>> SearchAsync<TSearchDto>(TSearchDto searchDto, List<Expression<Func<T, object>>>? includes = null)
        {
            IQueryable<T> query = await GetDataAndApplyIncludes(includes);

            query = await GenericSearch(searchDto, query);

            return await query.ToListAsync();
        }

        public async Task<(IEnumerable<TDto> Data, int TotalCount)> SearchPagedMappedAsync<TDto, TSearchDto>(
                           TSearchDto searchDto,
                           int page,
                           int pageSize,
                           List<Expression<Func<T, object>>>? includes = null)
        {

            IQueryable<T> query = await GetDataAndApplyIncludes(includes);

            query = await GenericSearch(searchDto, query);

            var totalCount = await query.CountAsync();

            var data = await query
                .ProjectTo<TDto>(_mapper.ConfigurationProvider)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, totalCount);
        }

        public async Task<(IEnumerable<T> Data, int TotalCount)> SearchPagedAsync<TSearchDto>(
                           TSearchDto searchDto,
                           int page,
                           int pageSize,
                           List<Expression<Func<T, object>>>? includes = null)
        {

            IQueryable<T> query = await GetDataAndApplyIncludes(includes);
            query = await GenericSearch(searchDto, query);

            var totalCount = await query.CountAsync();

            var data = await query.Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, totalCount);
        }

        private async Task<IQueryable<T>> GetDataAndApplyIncludes(List<Expression<Func<T, object>>>? includes)
        {
            IQueryable<T> query = _dbSet.AsQueryable();
            if (includes != null)
            {
                foreach (var include in includes)
                    query = query.Include(include);
            }
            return query;
        }

        private async Task<IQueryable<T>> GenericSearch<TSearchDto>(TSearchDto searchDto, IQueryable<T> query)
        {
            var entityType = typeof(T);
            var dtoType = typeof(TSearchDto);
            var param = Expression.Parameter(entityType, "x");

            // Build dynamic expression
            Expression? predicate = null;
            var dtoProperties = dtoType.GetProperties();

            foreach (var prop in dtoProperties)
            {
                //check from values

                var value = prop.GetValue(searchDto);

                if (value == null)
                    continue;

                if (value is DateTime dt && dt == DateTime.MinValue)
                    continue;


                string basePropName;
                bool isRangeFrom = false;
                bool isRangeTo = false;

                if (prop.Name.EndsWith("From"))
                {
                    isRangeFrom = true;
                    basePropName = prop.Name[..^4];
                }
                else if (prop.Name.EndsWith("To"))
                {
                    isRangeTo = true;
                    basePropName = prop.Name[..^2];
                }
                else
                {
                    basePropName = prop.Name;
                }

                var entityProp = entityType.GetProperty(basePropName);
                if (entityProp == null)
                    continue;

                //to build my experssion 
                //{x.MyProperty}
                var member = Expression.Property(param, entityProp);
                Expression constant = Expression.Constant(value);
                //check if valid types is compared
                if (member.Type != constant.Type)
                    constant = Expression.Convert(constant, member.Type);

                Expression comparison;

                if (isRangeFrom)
                {
                    comparison = Expression.GreaterThanOrEqual(member, constant);
                }
                else if (isRangeTo)
                {
                    comparison = Expression.LessThanOrEqual(member, constant);
                }
                else if (member.Type == typeof(string))
                {
                    //{(x.Property != null)}
                    var notNull = Expression.NotEqual(member, Expression.Constant(null));

                    //{ x.Property.Contains("string")}
                    var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
                    var contains = Expression.Call(member, containsMethod, constant);
                    //final look =>> {((x.Property != null) AndAlso x.Property.Contains("string"))}
                    comparison = Expression.AndAlso(notNull, contains);
                }
                //else if (Nullable.GetUnderlyingType(member.Type) != null)
                //{

                //    var hasValue = Expression.Property(member, "HasValue");
                //    var valueProperty = Expression.Property(member, "Value");
                //    var equal = Expression.Equal(valueProperty, constant);
                //    comparison = Expression.AndAlso(hasValue, equal);
                //}
                else
                {
                    comparison = Expression.Equal(member, constant);
                }

                predicate = predicate == null
                    ? comparison
                    : Expression.AndAlso(predicate, comparison);

                if (prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    break;
            }

            if (predicate != null)
            {
                var lambda = Expression.Lambda<Func<T, bool>>(predicate, param);
                query = query.Where(lambda);
            }

            return query;
        }
        #endregion

        #region ordinary search
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

        #endregion







        


    }
}
