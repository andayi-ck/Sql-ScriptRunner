using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KabarakScriptRunner.Data.Models;

namespace KabarakScriptRunnerApp
{
    public partial class ScriptFinderWindow : Window
    {
        private readonly List<Script> _allScripts;
        private readonly List<Department> _departments;

        public Script? SelectedScript { get; private set; }

        public ScriptFinderWindow(IEnumerable<Script> scripts, IEnumerable<Department> departments)
        {
            InitializeComponent();
            _allScripts = scripts.ToList();
            _departments = departments.ToList();

            // Load departments into the combo, with a "(All Departments)" option prepended
            var deptList = new List<Department>
            {
                new Department { DepartmentId = -1, DepartmentName = "(All Departments)" }
            };
            deptList.AddRange(_departments);
            cmbDepartment.ItemsSource = deptList;
            cmbDepartment.SelectedIndex = 0;

            ApplyFilter();

            Loaded += (_, _) => txtSearch.Focus();
        }

        // ============================================
        // FILTER LOGIC
        // ============================================

        private void ApplyFilter()
        {
            var query = txtSearch.Text.Trim().ToLowerInvariant();
            var filterDept = chkFilterByDept.IsChecked == true;
            var selectedDept = cmbDepartment.SelectedItem as Department;

            IEnumerable<Script> filtered = _allScripts;

            // If "Filter By Department" is on and a real department is chosen,
            // restrict to that department.
            if (filterDept && selectedDept != null && selectedDept.DepartmentId != -1)
            {
                filtered = filtered.Where(s => s.DepartmentId == selectedDept.DepartmentId);
            }

            // Apply text query against Name, Description, Category, DepartmentName
            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(s =>
                    (s.Name ?? "").ToLowerInvariant().Contains(query) ||
                    (s.Description ?? "").ToLowerInvariant().Contains(query) ||
                    (s.Category ?? "").ToLowerInvariant().Contains(query) ||
                    (s.DepartmentName ?? "").ToLowerInvariant().Contains(query));
            }

            var list = filtered.ToList();
            lstResults.ItemsSource = list;

            lblResultsCount.Text = $"{list.Count} of {_allScripts.Count} scripts";

            if (list.Count > 0)
                lstResults.SelectedIndex = 0;
        }

        // ============================================
        // EVENT HANDLERS
        // ============================================

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblSearchHint.Visibility = string.IsNullOrEmpty(txtSearch.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            ApplyFilter();
        }

        private void chkFilterByDept_Changed(object sender, RoutedEventArgs e)
        {
            cmbDepartment.IsEnabled = chkFilterByDept.IsChecked == true;
            ApplyFilter();
        }

        private void cmbDepartment_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void lstResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            AcceptSelection();
        }

        private void lstResults_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AcceptSelection();
        }

        private void btnSelect_Click(object sender, RoutedEventArgs e) => AcceptSelection();

        private void AcceptSelection()
        {
            if (lstResults.SelectedItem is Script s)
            {
                SelectedScript = s;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please select a script from the list first.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}