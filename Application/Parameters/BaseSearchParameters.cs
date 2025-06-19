using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Parameters
{
    public class BaseSearchParameters
    {
        public DateTime? CreatedAtTo { get; set; } = DateTime.Now;
        public DateTime? CreatedAtFrom { get; set; }
    }
}
