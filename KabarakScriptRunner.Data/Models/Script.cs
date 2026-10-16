using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KabarakScriptRunner.Data.Models
{
    public class Script : INotifyPropertyChanged
    {
        private int? _departmentId;

        public int ScriptId { get; set; }
        public int Version { get; set; } = 1;   // NEW
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string SqlText { get; set; } = string.Empty;
        public string? Category { get; set; }
        public bool NeedsDateFrom { get; set; }
        public bool NeedsDateTo { get; set; }
        public bool IsActive { get; set; }

        public int? DepartmentId
        {
            get => _departmentId;
            set
            {
                if (_departmentId != value)
                {
                    _departmentId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? DepartmentName { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public override string ToString() => Name;
    }
}