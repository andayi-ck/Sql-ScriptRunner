using System;
using System.Windows;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunner.Data.Models;

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

            // Reset validation status when the user types
            txtSqlText.TextChanged += (_, _) => { _isSyntaxValidated = false; btnSave.IsEnabled = false; lblValidationStatus.Text = ""; };
        }

        private async System.Threading.Tasks.Task LoadDepartmentsAsync()
        {
            var departments = await _scriptRepository.GetDepartmentsAsync();
            cmbDepartment.ItemsSource = departments;
            cmbDepartment.SelectedIndex = 0;
        }

        private async void btnTestSyntax_Click(object sender, RoutedEventArgs e)
        {
            var sql = txtSqlText.Text.Trim();
            if (string.IsNullOrWhiteSpace(sql))
            {
                lblValidationStatus.Text = "SQL is empty.";
                lblValidationStatus.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            lblValidationStatus.Text = "Validating...";
            lblValidationStatus.Foreground = System.Windows.Media.Brushes.Gray;

            // Run the static ScriptValidator first (blocks dangerous keywords)
            var staticResult = KabarakScriptRunner.Core.Services.ScriptValidator.Validate(sql);
            if (!staticResult.IsValid)
            {
                lblValidationStatus.Text = "❌ Blocked: " + string.Join(", ", staticResult.Errors);
                lblValidationStatus.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            // If it passes the static check, test the actual SQL syntax against the database
            var (isValid, error) = await _scriptRepository.ValidateSqlSyntaxAsync(sql);

            if (isValid)
            {
                lblValidationStatus.Text = "✅ Syntax is valid! You can now save.";
                lblValidationStatus.Foreground = System.Windows.Media.Brushes.Green;
                _isSyntaxValidated = true;
                btnSave.IsEnabled = true;
            }
            else
            {
                lblValidationStatus.Text = "❌ Syntax Error: " + error;
                lblValidationStatus.Foreground = System.Windows.Media.Brushes.Red;
                _isSyntaxValidated = false;
                btnSave.IsEnabled = false;
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
                MessageBox.Show($"Failed to save script:\n\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}