using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Parameters
{
    public class SearchProductParameters: PagingParameters
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public int? StockQuantity { get; set; }
        public int? CategoryId { get; set; }
        public bool? IsAvailable { get; set; }
        public DateTime? CreatedAtTo { get; set; } = DateTime.MinValue;
        public DateTime? CreatedAtFrom { get; set; } = DateTime.MinValue;
    }
}
