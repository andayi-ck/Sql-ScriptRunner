using System;

namespace KabarakScriptRunner.Data.Models
{
    public class ScriptVersion
    {
        public long VersionId { get; set; }
        public int ScriptId { get; set; }
        public int Version { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string SqlText { get; set; } = string.Empty;
        public string? Category { get; set; }
        public int? DepartmentId { get; set; }
        public bool NeedsDateFrom { get; set; }
        public bool NeedsDateTo { get; set; }
        public bool IsActive { get; set; }
        public DateTime ArchivedAt { get; set; }
        public string? ArchivedBy { get; set; }
    }
}