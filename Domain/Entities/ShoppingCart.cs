using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities
{
    public class ShoppingCart : BaseEntity
    {
        [Required]
        public string UserId { get; set; } // Will be linked to Identity User
        
        public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }

    public class CartItem : BaseEntity
    {
        public int ShoppingCartId { get; set; }
        public virtual ShoppingCart ShoppingCart { get; set; }
        
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }
        
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
    }
}
