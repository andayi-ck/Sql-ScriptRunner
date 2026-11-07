using System;
using System.Windows;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunner.Data.Models;
using Serilog; // F-07 Fix

namespace KabarakScriptRunnerApp
{
    public partial class ScriptEditorWindow : Window
    {
        private readonly ScriptRepository _scriptRepository;
        private bool _isSyntaxValidated = false;

        public ScriptEditorWindow(ScriptRepository scriptRepository)
        {
            InitializeComponent();
            _scriptRepository = scriptRepository;
            Loaded += async (_, _) => await LoadDepartmentsAsync();

            txtSqlText.TextChanged += (_, _) => { _isSyntaxValidated = false; btnSave.IsEnabled = false; lblValidationStatus.Text = ""; };
        }

        private async System.Threading.Tasks.Task LoadDepartmentsAsync()
        {
            try
            {
                var departments = await _scriptRepository.GetDepartmentsAsync();
                cmbDepartment.ItemsSource = departments;
                if (cmbDepartment.Items.Count > 0) cmbDepartment.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load departments in Script Editor");
            }
        }

        private void btnTestSyntax_Click(object sender, RoutedEventArgs e)
        {
            var sql = txtSqlText.Text.Trim();

            // Use the new local parser
            var (isValid, errors) = KabarakScriptRunner.Core.Services.ScriptValidator.Validate(sql);

            if (isValid)
            {
                lblValidationStatus.Text = "✅ Syntax is valid! You can now save.";
                lblValidationStatus.Foreground = System.Windows.Media.Brushes.Green;
                _isSyntaxValidated = true;
                btnSave.IsEnabled = true;
            }
            else
            {
                // Note: The parser errors are safe to show because they are generic syntax errors, not DB schema leaks.
                lblValidationStatus.Text = "❌ Error: " + string.Join("\n", errors);
                lblValidationStatus.Foreground = System.Windows.Media.Brushes.Red;
                _isSyntaxValidated = false;
                btnSave.IsEnabled = false;
            }
        }

        private void txtSqlText_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var lineCount = txtSqlText.LineCount;
            if (lineCount < 1) lineCount = 1;

            var numbers = string.Join("\n", System.Linq.Enumerable.Range(1, lineCount));
            txtLineNumbers.Text = numbers;

            lblCharCount.Text = $"{txtSqlText.Text.Length} characters";

            _isSyntaxValidated = false;
            btnSave.IsEnabled = false;
            lblValidationStatus.Text = "";
        }

        private void txtSqlText_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                var scrollViewer = (System.Windows.Controls.ScrollViewer)e.OriginalSource;
                if (scrollViewer.Name == "PART_ContentHost")
                {
                    // Scroll sync logic if needed
                }
            }
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSyntaxValidated)
            {
                MessageBox.Show("Please test the syntax first before saving.", "Validation Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var name = txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Script Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var (isDuplicate, reason) = await _scriptRepository.CheckScriptDuplicateAsync(name, txtSqlText.Text.Trim());
                if (isDuplicate)
                {
                    MessageBox.Show($"Duplicate Script Detected:\n\n{reason}", "Duplicate Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dept = cmbDepartment.SelectedItem as Department;
                var targetDb = (cmbTargetDatabase.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "SQLServer";

                await _scriptRepository.CreateScriptAsync(
                    name,
                    txtDescription.Text.Trim(),
                    txtSqlText.Text.Trim(),
                    txtCategory.Text.Trim(),
                    targetDb,
                    chkNeedsDateFrom.IsChecked == true,
                    chkNeedsDateTo.IsChecked == true,
                    dept?.DepartmentId
                );

                MessageBox.Show("Script saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                // F-07 Fix: Log the real error for you, show a safe message to the user
                Log.Error(ex, "Failed to save script: {ScriptName}", name);
                MessageBox.Show(
                    "An error occurred while saving the script. Please check your SQL syntax or contact the administrator.",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}