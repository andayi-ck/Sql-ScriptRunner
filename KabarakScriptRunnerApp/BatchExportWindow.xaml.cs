using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KabarakScriptRunner.Core.Services;
using KabarakScriptRunner.Data.Models;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunnerApp.Services;
using Microsoft.Win32;
using Serilog;

namespace KabarakScriptRunnerApp
{
    public partial class BatchExportWindow : Window
    {
        private readonly ScriptRepository _scriptRepository;
        private readonly User _currentUser;

        private List<ScriptRow> _allScripts = new();
        private CancellationTokenSource? _batchCts;

        // CN-03: Re-entry guard, independent of button state.
        private bool _isBatchRunning;

        public BatchExportWindow(ScriptRepository scriptRepository, User currentUser)
        {
            InitializeComponent();
            _scriptRepository = scriptRepository;
            _currentUser = currentUser;

            // Default date range: last 12 months up to today
            dtpFrom.SelectedDate = DateTime.Today.AddMonths(-12);
            dtpTo.SelectedDate = DateTime.Today;

            // Default output folder from settings
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var outputFolder = await _scriptRepository.GetSettingAsync("OutputFolder");
                if (!string.IsNullOrEmpty(outputFolder))
                    txtOutputFolder.Text = outputFolder;
                else
                    txtOutputFolder.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                var scripts = (await _scriptRepository.GetScriptsAsync(null, isAdmin: true)).ToList();

                _allScripts = scripts.Select(s => new ScriptRow
                {
                    ScriptId = s.ScriptId,
                    Name = s.Name,
                    Description = s.Description,
                    Category = s.Category,
                    DepartmentName = s.DepartmentName,
                    Version = s.Version,
                    SqlText = s.SqlText,
                    NeedsDateFrom = s.NeedsDateFrom,
                    NeedsDateTo = s.NeedsDateTo,
                    IsSelected = false
                }).ToList();

                dgScripts.ItemsSource = _allScripts;

                lblStatus.Text = $"{_allScripts.Count} scripts available.";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load scripts in BatchExportWindow");
                MessageBox.Show("Failed to load scripts. Please try again.",
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================
        // FILTERING
        // ============================================
        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = txtFilter.Text.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(query))
            {
                dgScripts.ItemsSource = _allScripts;
            }
            else
            {
                var filtered = _allScripts.Where(s =>
                    (s.Name ?? "").ToLowerInvariant().Contains(query) ||
                    (s.Description ?? "").ToLowerInvariant().Contains(query) ||
                    (s.Category ?? "").ToLowerInvariant().Contains(query) ||
                    (s.DepartmentName ?? "").ToLowerInvariant().Contains(query))
                    .ToList();
                dgScripts.ItemsSource = filtered;
            }
        }

        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            var visible = dgScripts.ItemsSource as IEnumerable<ScriptRow>;
            if (visible == null) return;

            bool allSelected = visible.All(s => s.IsSelected);
            foreach (var s in visible)
                s.IsSelected = !allSelected;

            dgScripts.Items.Refresh();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select output folder for batch export",
                InitialDirectory = Directory.Exists(txtOutputFolder.Text)
                    ? txtOutputFolder.Text
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
                txtOutputFolder.Text = dialog.FolderName;
        }

        // ============================================
        // RUN BATCH
        // ============================================
        private async void btnRunBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchRunning) return;
            _isBatchRunning = true;

            try
            {
                // Validate
                var selected = _allScripts.Where(s => s.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show("Please select at least one script.",
                        "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var outputFolder = txtOutputFolder.Text.Trim();
                if (string.IsNullOrWhiteSpace(outputFolder))
                {
                    MessageBox.Show("Please select an output folder.",
                        "No Output Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try { Directory.CreateDirectory(outputFolder); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Cannot use output folder:\n\n{ex.Message}",
                        "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (dtpFrom.SelectedDate == null || dtpTo.SelectedDate == null)
                {
                    MessageBox.Show("Please select the date range.",
                        "Missing Dates", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dateFrom = dtpFrom.SelectedDate.Value;
                var dateTo = dtpTo.SelectedDate.Value;

                if (dateTo > DateTime.Today)
                {
                    MessageBox.Show("Date To cannot be in the future.",
                        "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (dateTo < dateFrom)
                {
                    MessageBox.Show("Date To cannot be earlier than Date From.",
                        "Invalid Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check scheduling
                DateTime? scheduledAt = null;
                if (dtpScheduleDate.SelectedDate.HasValue &&
                    !string.IsNullOrWhiteSpace(txtScheduleTime.Text))
                {
                    if (TimeSpan.TryParse(txtScheduleTime.Text, out var time))
                    {
                        scheduledAt = dtpScheduleDate.SelectedDate.Value.Date + time;
                        if (scheduledAt <= DateTime.Now)
                        {
                            MessageBox.Show("The scheduled time is in the past. Please pick a future time.",
                                "Invalid Schedule", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("The time format is invalid. Use HH:mm, e.g. 02:00.",
                            "Invalid Time", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Confirm
                var confirm = MessageBox.Show(
                    $"You are about to run {selected.Count} scripts.\n\n" +
                    $"Date range: {dateFrom:yyyy-MM-dd} to {dateTo:yyyy-MM-dd}\n" +
                    $"Output folder: {outputFolder}\n" +
                    (scheduledAt.HasValue ? $"Scheduled for: {scheduledAt:yyyy-MM-dd HH:mm}\n" : "Immediate execution\n") +
                    "\nProceed?",
                    "Confirm Batch",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes) return;

                // CN-05: Create the CTS ONCE, here, and own it until the finally below.
                // ExecuteBatchAsync no longer creates its own — it receives the token.
                _batchCts = new CancellationTokenSource();
                var token = _batchCts.Token;

                try
                {
                    // If scheduled, wait
                    if (scheduledAt.HasValue)
                    {
                        var wait = scheduledAt.Value - DateTime.Now;
                        lblStatus.Text = $"Scheduled. Waiting until {scheduledAt:yyyy-MM-dd HH:mm} ({(int)wait.TotalMinutes} minutes from now)...";

                        try
                        {
                            await Task.Delay(wait, token);
                        }
                        catch (OperationCanceledException)
                        {
                            lblStatus.Text = "Scheduled batch cancelled.";
                            return;
                        }
                    }

                    // Run
                    await ExecuteBatchAsync(selected, dateFrom, dateTo, outputFolder, token);
                }
                finally
                {
                    // CN-03 + CN-05: Single owner disposes; null BEFORE dispose so
                    // any in-flight Cancel sees null and bails.
                    var cts = _batchCts;
                    _batchCts = null;
                    try { cts?.Dispose(); } catch { /* already disposed */ }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Batch run failed unexpectedly");
                MessageBox.Show("The batch export failed unexpectedly. Please check the logs.",
                    "Batch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isBatchRunning = false;
            }
        }

        private async Task ExecuteBatchAsync(List<ScriptRow> selected, DateTime dateFrom, DateTime dateTo, string outputFolder, CancellationToken token)
        {
            btnRunBatch.IsEnabled = false;
            btnSelectAll.IsEnabled = false;
            btnBrowse.IsEnabled = false;
            btnCancel.Content = "Cancel";

            prgBatch.Value = 0;
            prgBatch.Maximum = selected.Count;

            int ok = 0, fail = 0;
            var startedAt = DateTime.Now;

            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        lblStatus.Text = $"Cancelled after {i} of {selected.Count} scripts.";
                        break;
                    }

                    var row = selected[i];
                    lblStatus.Text = $"Running {i + 1} of {selected.Count}: {row.Name}";

                    try
                    {
                        var script = new Script
                        {
                            ScriptId = row.ScriptId,
                            Name = row.Name,
                            Description = row.Description,
                            SqlText = row.SqlText,
                            Category = row.Category,
                            Version = row.Version,
                            NeedsDateFrom = row.NeedsDateFrom,
                            NeedsDateTo = row.NeedsDateTo
                        };

                        var runId = await _scriptRepository.StartScriptRunAsync(
                            _currentUser.UserId, script.ScriptId, dateFrom, dateTo);

                        var table = await ScriptExecutorService.ExecuteScriptAsync(
                            App.ConnectionString,
                            script.SqlText,
                            script.NeedsDateFrom ? dateFrom : null,
                            script.NeedsDateTo ? dateTo : null,
                            token);

                        var filePath = ExcelExportService.ExportToExcel(table, outputFolder, script.Name);

                        await _scriptRepository.FinishScriptRunSuccessAsync(runId, table.Rows.Count, filePath);

                        await _scriptRepository.LogAdminActionAsync(
                            _currentUser.UserId, "BatchScriptRan", "Script",
                            script.ScriptId.ToString(),
                            $"Batch: '{script.Name}' rows={table.Rows.Count} output={Path.GetFileName(filePath)}");

                        ok++;
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        try
                        {
                            await _scriptRepository.LogAdminActionAsync(
                                _currentUser.UserId, "BatchScriptFailed", "Script",
                                row.ScriptId.ToString(),
                                $"Batch: '{row.Name}' failed: {ex.Message}");
                        }
                        catch (Exception logEx)
                        {
                            Log.Warning(logEx, "Failed to write BatchScriptFailed audit row");
                        }
                    }

                    prgBatch.Value = i + 1;
                }
            }
            finally
            {
                btnRunBatch.IsEnabled = true;
                btnSelectAll.IsEnabled = true;
                btnBrowse.IsEnabled = true;

                var elapsed = DateTime.Now - startedAt;
                lblStatus.Text = $"Batch complete in {elapsed:mm\\:ss}. Succeeded: {ok}, Failed: {fail}. Folder: {outputFolder}";

                if (fail == 0)
                    ToastService.Success($"Batch complete: {ok} file(s) exported.");
                else
                    ToastService.Warning($"Batch done: {ok} succeeded, {fail} failed.");

                try { System.Diagnostics.Process.Start("explorer.exe", outputFolder); }
                catch (Exception ex) { Log.Warning(ex, "Could not open explorer for output folder"); }
            }
        }

        /// <summary>
        /// CN-03: Capture _batchCts into a local across the modal. If the batch
        /// finishes while the modal is open, the field is null — bail cleanly.
        /// </summary>
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            var cts = _batchCts;
            if (cts != null && !cts.IsCancellationRequested)
            {
                var confirm = MessageBox.Show(
                    "Cancel the running batch?\n\n" +
                    "Scripts already completed will keep their Excel files.",
                    "Cancel Batch", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes) return;

                try
                {
                    cts.Cancel();
                    lblStatus.Text = "Cancellation requested...";
                }
                catch (ObjectDisposedException)
                {
                    // Batch finished while the modal was open — nothing to do.
                }
            }
            else
            {
                // No batch running → treat as Close
                Close();
            }
        }

        /// <summary>
        /// CN-03: Same local-capture pattern as btnCancel_Click.
        /// </summary>
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            var cts = _batchCts;
            if (cts != null && !cts.IsCancellationRequested)
            {
                var confirm = MessageBox.Show(
                    "A batch is currently running. Are you sure you want to close this window?",
                    "Batch Running", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes) return;

                try { cts.Cancel(); }
                catch (ObjectDisposedException) { /* already finished */ }
            }
            Close();
        }
    }

    // ============================================
    // ROW VIEW MODEL for the DataGrid — UNCHANGED
    // ============================================
    public class ScriptRow : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;

        public int ScriptId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? DepartmentName { get; set; }
        public int Version { get; set; } = 1;
        public string SqlText { get; set; } = "";
        public bool NeedsDateFrom { get; set; }
        public bool NeedsDateTo { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}