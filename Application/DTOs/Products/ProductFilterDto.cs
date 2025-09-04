namespace Application.DTOs.Products
{
    public class ProductFilterDto
    {
        public string SearchTerm { get; set; } = string.Empty;
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? CategoryId { get; set; }
        public bool? IsAvailable { get; set; }
        public bool? IsBestSeller { get; set; }
        public bool? InStock { get; set; }
        public string SortBy { get; set; } = "newest";
        public bool SortDescending { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
