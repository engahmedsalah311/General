using Application.DTOs;
using Application.Wrapper;
using Domain.Entities;
using System.Linq.Expressions;

namespace Application.Interfaces
{
    public interface IGenericService<T, TDto>
        where T : BaseEntity
        where TDto : GeneralDto
    {
        Task<Result<IEnumerable<TDto>>> GetAllAsync();
        Task<Result<TDto>> GetByIdAsync(int id);
        Task<Result<TDto>> AddAsync(TDto entity);
        Task<Result<int>> DeleteAsync(int id);
        Task<Result<TDto>> UpdateAsync(int id, TDto entityDto);
        
    }
}
