using System;
using System.Collections.Generic;
using System.Text;

namespace KabarakScriptRunner.Data.Models
{
    public class UserSummary
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool IsActive { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
    }
}