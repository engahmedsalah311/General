using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class ProductColours
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int ColourId { get; set; }
        public Colour Colour { get; set; }
        public int Quantity { get; set; } = 0;
    }
}
