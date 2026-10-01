using System;
using System.Collections.Generic;
using System.Text;

namespace KabarakScriptRunner.Data.Models
{
    public class Department
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public override string ToString() => DepartmentName;
    }
}