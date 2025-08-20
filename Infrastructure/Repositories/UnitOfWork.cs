using Application.Interfaces;
using AutoMapper;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Infrastructure.Repositories
{
    public class UnitOfWork:IUnitOfWork
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, object> _repositories = new();
        private Hashtable _serviceRepository;
        private IDbContextTransaction? _transaction;

        public UnitOfWork(AppDbContext context,IMapper mapper,IServiceProvider serviceProvider){
            _context = context;
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _serviceProvider = serviceProvider;
        }

        public IRepository<T> Repository<T>() where T : class
        {
            var type = typeof(T);
            if (!_repositories.ContainsKey(type))
                _repositories[type] = new Repository<T>(_context,_mapper);

            return (IRepository<T>)_repositories[type];
        }
        public TRepository RepositoryOf<TRepository> () => _serviceProvider.GetRequiredService<TRepository>();
        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

        public async Task CommitAsync()
        {
            try
            {

                await _context.SaveChangesAsync();
            }catch(Exception ex)
            {
                throw ex;
            }
        }
        public async Task BeginTransaction()
        {
            if(_transaction == null)
            _transaction = await _context.Database.BeginTransactionAsync();
        }
        public async Task CommitTransactionAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
                if (_transaction != null)
                {
                    await _transaction.CommitAsync();
                    await _transaction.DisposeAsync();
                    _transaction = null;
                }
            }
            catch(Exception ex)
            {
                await Rollback();
                throw ex;
            }
        }
        public async Task Rollback()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }

}
