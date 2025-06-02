using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Parameters
{
    public class SearchProductParameters: PagingParameters
    {
        public string? SearchTerm { get; set; }
        public string? Category { get; set; }
        public string? Brand { get; set; }
        public string? Name { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool IncludeOutOfStock { get; set; } = false;
    }
}
