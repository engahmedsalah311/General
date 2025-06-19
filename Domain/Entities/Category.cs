
using System;
using Domain.Entities;

namespace Domain.Entities
{
    public class Category:BaseEntity
    {
        // Add your entity properties here
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

    }
}