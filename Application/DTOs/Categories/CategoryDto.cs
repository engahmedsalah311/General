using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Categories
{
    public class CategoryDto
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot be longer than 100 characters")]
        public string Name { get; set; }
        
        [StringLength(500, ErrorMessage = "Description cannot be longer than 500 characters")]
        public string Description { get; set; }
        
        public bool IsActive { get; set; } = true;
        public int ProductCount { get; set; }
    }

    public class CreateCategoryDto
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot be longer than 100 characters")]
        public string Name { get; set; }
        
        [StringLength(500, ErrorMessage = "Description cannot be longer than 500 characters")]
        public string Description { get; set; }
        
        public bool IsActive { get; set; } = true;
    }

    public class UpdateCategoryDto : CreateCategoryDto
    {
        [Required(ErrorMessage = "Category ID is required")]
        public int Id { get; set; }
    }
}
