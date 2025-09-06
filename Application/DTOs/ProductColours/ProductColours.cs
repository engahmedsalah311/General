using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.ProductColours
{
    public class ProductColoursDto
    {
        public int ColourId { get; set; }
        public ColoursDto Colour { get; set; }
        public int Quantity { get; set; }

    }
}
