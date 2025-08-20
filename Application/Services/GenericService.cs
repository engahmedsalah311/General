using Application.DTOs;
using Application.Extentions;
using Application.Interfaces;
using Application.Wrapper;
using AutoMapper;
using Domain.Entities;
using System.Linq.Expressions;

namespace Application.Services
{
    public class GenericService<T, TDto> : IGenericService<T, TDto>
        where T : BaseEntity
        where TDto : GeneralDto
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected readonly IMapper _mapper;
        protected readonly IRepository<T> _repository;

        public GenericService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _repository = _unitOfWork.Repository<T>();
        }

        public virtual async Task<Result<IEnumerable<TDto>>> GetAllAsync()
        {
            try
            {
                var entities = await _repository.GetAllAsList();
                var dtos = _mapper.Map<IEnumerable<TDto>>(entities);
                return Result<IEnumerable<TDto>>.SuccessResult(dtos);
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<TDto>>.FailureResult(ex.Message);
            }
        }

        public virtual async Task<Result<TDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return Result<TDto>.FailureResult("Entity Not Found!");

                var dto = _mapper.Map<TDto>(entity);
                return  Result<TDto>.SuccessResult(dto);
            }
            catch (Exception ex)
            {
                return Result<TDto>.FailureResult(ex.Message);
            }
        }

        public virtual async Task<Result<TDto>> AddAsync(TDto entityDto)
        {
            try
            {
                var entity = _mapper.Map<T>(entityDto);
                await _repository.AddAsync(entity);
                await _unitOfWork.CommitAsync();

                entityDto = _mapper.Map<TDto>(entity);
                return Result<TDto>.SuccessResult(entityDto);
            }
            catch (Exception ex)
            {
                return Result<TDto>.FailureResult(ex.Message);
            }
        }

        public virtual async Task<Result<TDto>> UpdateAsync(int id, TDto entityDto)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return Result<TDto>.FailureResult("Entity Not Found!");

                _mapper.Map(entityDto, entity);
                _repository.Update(entity);
                await _unitOfWork.SaveChangesAsync();

                return Result<TDto>.SuccessResult(entityDto);
            }
            catch (Exception ex)
            {
                return Result<TDto>.FailureResult(ex.Message);
            }
        }

        public virtual async Task<Result<int>> DeleteAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return Result<int>.FailureResult("Entity Not Found!");

                _repository.Delete(entity);
                await _unitOfWork.SaveChangesAsync();

                return Result<int>.SuccessResult(id,"Entity Deleted Successfully!");
            }
            catch (Exception ex)
            {
                return Result<int>.FailureResult(ex.Message);
            }
        }

        

    }

}
