using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using KabarakScriptRunner.Data.Models;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunnerApp.Services;

namespace KabarakScriptRunnerApp
{
    public partial class AssignScriptsWindow : Window
    {
        private readonly ScriptRepository _scriptRepository;
        private readonly string _currentAdminUserId;

        public List<Department> Departments { get; private set; } = new();
        public List<Script> Scripts { get; private set; } = new();

        public AssignScriptsWindow(ScriptRepository scriptRepository, string currentAdminUserId)
        {
            InitializeComponent();
            _scriptRepository = scriptRepository;
            _currentAdminUserId = currentAdminUserId;
            DataContext = this;

            Loaded += async (_, _) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                var depts = (await _scriptRepository.GetDepartmentsAsync()).ToList();
                depts.Insert(0, new Department
                {
                    DepartmentId = 0,
                    DepartmentName = "(Unassigned)"
                });
                Departments = depts;

                Scripts = (await _scriptRepository.GetScriptsAsync(null, true)).ToList();

                foreach (var s in Scripts)
                    if (s.DepartmentId == null) s.DepartmentId = 0;

                DataContext = null;
                DataContext = this;

                dgScripts.ItemsSource = Scripts;
                lblStatus.Text = $"{Scripts.Count} scripts loaded.";
            }
            catch (Exception ex)
            {
                // EH-01: safe text only.
                var safe = SafeErrorMessage.For(ex, "AssignScripts.Load");
                MessageBox.Show($"Failed to load data:\n\n{safe}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (Scripts == null || Scripts.Count == 0) return;

            try
            {
                int changed = 0;
                int unchanged = 0;

                foreach (var script in Scripts)
                {
                    int? newDeptId = script.DepartmentId == 0 ? null : script.DepartmentId;

                    var existing = await _scriptRepository.GetScriptByIdAsync(script.ScriptId);
                    int? oldDeptId = existing?.DepartmentId;

                    if (oldDeptId == newDeptId)
                    {
                        unchanged++;
                        continue;
                    }

                    await _scriptRepository.UpdateScriptDepartmentAsync(script.ScriptId, newDeptId);

                    string fromName = oldDeptId == null
                        ? "(Unassigned)"
                        : Departments.FirstOrDefault(d => d.DepartmentId == oldDeptId)?.DepartmentName
                          ?? $"Dept #{oldDeptId}";

                    string toName = newDeptId == null
                        ? "(Unassigned)"
                        : Departments.FirstOrDefault(d => d.DepartmentId == newDeptId)?.DepartmentName
                          ?? $"Dept #{newDeptId}";

                    await _scriptRepository.LogAdminActionAsync(
                        _currentAdminUserId,
                        "ScriptReassigned",
                        "Script",
                        script.ScriptId.ToString(),
                        $"Reassigned script '{script.Name}' from '{fromName}' to '{toName}'.");

                    changed++;
                }

                lblStatus.Text = $"Saved {changed} change(s); {unchanged} unchanged.";
                MessageBox.Show(
                    $"Successfully saved {changed} change(s).\n" +
                    $"({unchanged} script(s) were unchanged.)",
                    "Saved", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                // EH-01: safe text only.
                var safe = SafeErrorMessage.For(ex, "AssignScripts.Save");
                MessageBox.Show($"Failed to save:\n\n{safe}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}