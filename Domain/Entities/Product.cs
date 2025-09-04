using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities
{
    public class Product : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Name { get; set; }
        
        [MaxLength(1000)]
        public string Description { get; set; }
        
        [Required, Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        
        public string ImageUrl { get; set; }
        
        public int StockQuantity { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public int CategoryId { get; set; }
        public virtual Category Category { get; set; }
        
        // Additional properties
        public bool IsBestSeller { get; set; }
        public DateTime? LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
