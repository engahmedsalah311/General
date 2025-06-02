using Application.Wrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Extentions
{
    public static class PagingExtension
    {
        public static Pagination<T> ToPagedResultAsync<T>(this IQueryable<T> source, int pageNumber, int pageSize)
        {
            if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than 0.");
            if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than 0.");
            var totalCount = source.Count();
            var items = source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            var pagination = new Pagination<T>
            {
                Data = items,
                TotalCount = totalCount,
                PageSize = pageSize,
                PageNumber = pageNumber
            };
            return pagination;
        }
    }
}
