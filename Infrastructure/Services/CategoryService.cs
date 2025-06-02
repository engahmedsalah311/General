
using Application.DTOs;
using Application.Interfaces;
using Application.Parameters;
using Application.Wrapper;
using AutoMapper;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Services
{
    public class CategoryService : GenericService<Category, CategoryDto>, ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CategoryService(IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper)
        {
        }

        
    }
}