using System;
using Domain.Entities;
using Application.Parameters;

namespace Application.DTOs.Orders
{
    public class OrderFilterDto : PagingParameters
    {
        public int? Id { get; set; }
        public string OrderNumber { get; set; }
        public string UserId { get; set; }
        public OrderStatus? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public decimal? MinTotal { get; set; }
        public decimal? MaxTotal { get; set; }
        public string SortBy { get; set; } = "orderdate";
        public bool SortDescending { get; set; } = true;
    }
}
