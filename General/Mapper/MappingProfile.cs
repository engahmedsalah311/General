using Application.DTOs;
using Application.DTOs.ProductColours;
using Application.DTOs.Products;
using AutoMapper;
using Domain.Entities;

namespace General.Mapper
{
    public class MappingProfile:Profile
    {
        public MappingProfile()
        {
            CreateMap<Product, ProductDto>().ReverseMap();
            CreateMap<Colour, ColoursDto>().ReverseMap();
            CreateMap<ProductColours, ProductColoursDto > ().ReverseMap();
        }
    }
}
