using KabarakScriptRunner.Data.Models;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunnerApp.Services;
using KabarakScriptRunnerApp.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace KabarakScriptRunnerApp
{
    public partial class MainWindow : Window
    {
        private readonly ScriptRepository _scriptRepository;
        private User? _currentUser;
        private Department? _currentDepartment;
        private CancellationTokenSource? _runCts;

        private readonly ObservableCollection<LogEntry> _progressEntries = new();

        private List<Script> _allLoadedScripts = new();

        // NEW: Tracks the previous list to compare for changes
        private List<Script> _previousScripts = new();

        public bool LogoutRequested { get; private set; }

        public MainWindow(ScriptRepository scriptRepository)
        {
            InitializeComponent();
            _scriptRepository = scriptRepository;

            lstProgressLog.ItemsSource = _progressEntries;

            dtpTo.DisplayDateEnd = DateTime.Today;
            dtpTo.SelectedDateChanged += (_, _) =>
            {
                if (dtpTo.SelectedDate > DateTime.Today)
                    dtpTo.SelectedDate = DateTime.Today;
            };
        }

        public void SetCurrentUser(User user, Department department)
        {
            _currentUser = user;
            _currentDepartment = department;

            lblUserId.Text = user.UserId;
            lblUserName.Text = user.DisplayName;
            lblDepartment.Text = department.DepartmentName;

            bool isAdmin = user.IsAdmin;

            btnUsers.IsEnabled = isAdmin;
            btnAssign.IsEnabled = isAdmin;
            btnOptions.IsEnabled = isAdmin;
            btnAccessLog.IsEnabled = isAdmin;
            btnChangePassword.IsEnabled = isAdmin;
            btnBatchExport.IsEnabled = isAdmin;
            btnDashboard.IsEnabled = isAdmin;

            btnScripts.IsEnabled = true;

            Loaded += async (_, _) => await LoadInitialDataAsync();
        }

        private async Task LoadInitialDataAsync()
        {
            if (_currentUser == null) return;

            try
            {
                var outputFolder = await _scriptRepository.GetSettingAsync("OutputFolder");
                if (!string.IsNullOrEmpty(outputFolder))
                    txtOutputFolder.Text = outputFolder;

                // Pass 'false' for initial load (no spinner, no comparison)
                await LoadScriptsAsync(false);

                AppendProgress($"Logged in as {_currentUser.DisplayName} ({_currentUser.UserId}) — {_currentDepartment?.DepartmentName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data:\n\n{ex.Message}",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================
        // SCRIPTS: load + search filter
        // ============================================
        private async Task LoadScriptsAsync(bool isManualRefresh = false)
        {
            if (_currentUser == null) return;

            try
            {
                // Show loading overlay if this is a manual refresh
                if (isManualRefresh)
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    // Small delay so the user can actually see the spinner
                    await Task.Delay(500);
                }

                var newScripts = (await _scriptRepository.GetScriptsAsync(
                    _currentDepartment?.DepartmentId,
                    _currentUser.IsAdmin)).ToList();

                // Compare and notify if it's a manual refresh
                if (isManualRefresh && _previousScripts.Count > 0)
                {
                    CompareAndNotifyChanges(_previousScripts, newScripts);
                }

                _allLoadedScripts = newScripts;
                _previousScripts = newScripts; // Save for next time
                ApplyScriptFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing scripts:\n\n{ex.Message}",
                    "Refresh Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Hide loading overlay
                if (isManualRefresh)
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        // NEW: Compares old and new lists to find added, updated, and removed scripts
        private void CompareAndNotifyChanges(List<Script> oldList, List<Script> newList)
        {
            int added = 0;
            int updated = 0;
            int removed = 0;

            foreach (var newScript in newList)
            {
                var oldScript = oldList.FirstOrDefault(s => s.ScriptId == newScript.ScriptId);

                if (oldScript == null)
                {
                    added++;
                }
                else if (oldScript.Name != newScript.Name ||
                         oldScript.Description != newScript.Description ||
                         oldScript.Version != newScript.Version ||
                         oldScript.SqlText != newScript.SqlText ||
                         oldScript.DepartmentId != newScript.DepartmentId)
                {
                    updated++;
                }
            }

            foreach (var oldScript in oldList)
            {
                if (!newList.Any(s => s.ScriptId == oldScript.ScriptId))
                {
                    removed++;
                }
            }

            // Display results in the status line and via Toast
            if (added == 0 && updated == 0 && removed == 0)
            {
                lblProgressStatus.Text = "No changes detected. Scripts are up to date.";
                ToastService.Information("No changes detected.");
            }
            else
            {
                var message = "Updates detected: ";
                if (added > 0) message += $"{added} added, ";
                if (updated > 0) message += $"{updated} updated, ";
                if (removed > 0) message += $"{removed} removed, ";

                message = message.TrimEnd(',', ' ') + ".";

                lblProgressStatus.Text = message;
                ToastService.Success(message);
            }
        }

        private void ApplyScriptFilter()
        {
            var query = txtScriptSearch.Text.Trim().ToLowerInvariant();

            IEnumerable<Script> filtered = _allLoadedScripts;

            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(s =>
                    (s.Name ?? "").ToLowerInvariant().Contains(query) ||
                    (s.Description ?? "").ToLowerInvariant().Contains(query) ||
                    (s.Category ?? "").ToLowerInvariant().Contains(query) ||
                    (s.DepartmentName ?? "").ToLowerInvariant().Contains(query));
            }

            var list = filtered.ToList();
            lstScripts.ItemsSource = list;

            lblScriptCount.Text = list.Count == _allLoadedScripts.Count
                ? $"{_allLoadedScripts.Count} script(s)"
                : $"{list.Count} of {_allLoadedScripts.Count}";
        }

        private void txtScriptSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblScriptSearchHint.Visibility = string.IsNullOrEmpty(txtScriptSearch.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            ApplyScriptFilter();
        }

        // ============================================
        // AUDIT HELPER
        // ============================================
        private async Task LogAuditAsync(string userId, string actionType, string? targetType, string? targetId, string? details)
        {
            try
            {
                await _scriptRepository.LogAdminActionAsync(userId, actionType, targetType, targetId, details);
            }
            catch
            {
                // Never let an audit failure break the app.
            }
        }

        // ============================================
        // PROGRESS LOG
        // ============================================
        private void AppendProgress(string message)
        {
            string icon = "•";
            if (message.StartsWith("▶")) { icon = "▶"; message = message.Substring(1).TrimStart(); }
            else if (message.StartsWith("✔")) { icon = "✔"; message = message.Substring(1).TrimStart(); }
            else if (message.StartsWith("✘")) { icon = "✘"; message = message.Substring(1).TrimStart(); }
            else if (message.StartsWith("⚠")) { icon = "⚠"; message = message.Substring(1).TrimStart(); }

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                Icon = icon,
                Message = message
            };

            _progressEntries.Add(entry);

            while (_progressEntries.Count > 500)
                _progressEntries.RemoveAt(0);

            if (_progressEntries.Count > 0)
                lstProgressLog.ScrollIntoView(_progressEntries[^1]);
        }

        // ============================================
        // SELECTION FEEDBACK + ScriptViewed audit
        // ============================================
        private void lstScripts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int count = lstScripts.SelectedItems.Count;

            if (count == 0)
            {
                lblSelectedScript.Text = "(none)";
                lblSelectedScript.FontStyle = FontStyles.Italic;
                lblProgressStatus.Text = "";
                return;
            }

            if (count == 1)
            {
                var script = lstScripts.SelectedItem as Script;
                if (script != null)
                {
                    lblSelectedScript.Text = script.Name;
                    lblSelectedScript.FontStyle = FontStyles.Normal;
                    lblProgressStatus.Text = $"Ready to run: {script.Name}";

                    if (_currentUser != null)
                    {
                        _ = LogAuditAsync(_currentUser.UserId, "ScriptViewed", "Script",
                            script.ScriptId.ToString(),
                            $"Viewed script '{script.Name}' (v{script.Version}).");
                    }
                }
            }
            else
            {
                lblSelectedScript.Text = $"{count} scripts selected";
                lblSelectedScript.FontStyle = FontStyles.Normal;
                lblProgressStatus.Text = $"{count} scripts queued for execution.";
            }
        }

        // ============================================
        // OUTPUT FOLDER BROWSE
        // ============================================
        private void btnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select output folder for Excel exports",
                InitialDirectory = System.IO.Directory.Exists(txtOutputFolder.Text)
                    ? txtOutputFolder.Text
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
                txtOutputFolder.Text = dialog.FolderName;
        }

        // ============================================
        // ADMIN BUTTONS
        // ============================================
        private async void btnScripts_Click(object sender, RoutedEventArgs e)
        {
            var allScripts = lstScripts.ItemsSource as System.Collections.IEnumerable;
            if (allScripts == null)
            {
                MessageBox.Show("Scripts have not yet loaded. Try clicking 'Refresh Scripts'.",
                    "No Scripts", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var scriptList = allScripts.Cast<Script>().ToList();
            var departments = await _scriptRepository.GetDepartmentsAsync();

            var finder = new ScriptFinderWindow(scriptList, departments) { Owner = this };
            if (finder.ShowDialog() == true && finder.SelectedScript != null)
            {
                lstScripts.SelectedItem = finder.SelectedScript;
                lstScripts.ScrollIntoView(finder.SelectedScript);
                lstScripts.Focus();
            }
        }

        

        private void btnUsers_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || !_currentUser.IsAdmin) return;

            var window = new UsersWindow(_scriptRepository, _currentUser) { Owner = this };
            window.ShowDialog();
        }

        private void btnAssign_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || !_currentUser.IsAdmin) return;

            var window = new AssignScriptsWindow(_scriptRepository, _currentUser.UserId) { Owner = this };
            window.ShowDialog();

            _ = LoadScriptsAsync();
        }

        private void btnBatchExport_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || !_currentUser.IsAdmin) return;

            var window = new BatchExportWindow(_scriptRepository, _currentUser) { Owner = this };
            window.ShowDialog();
        }

        private void btnOptions_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || !_currentUser.IsAdmin) return;

            var window = new OptionsWindow(_scriptRepository) { Owner = this };
            window.ShowDialog();
        }

        private void btnAccessLog_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || !_currentUser.IsAdmin) return;

            var dashboardRepo = App.ServiceProvider.GetRequiredService<DashboardRepository>();
            var window = new AccessLogWindow(_scriptRepository, dashboardRepo) { Owner = this };
            window.ShowDialog();
        }

        private void btnDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || !_currentUser.IsAdmin) return;

            var repo = App.ServiceProvider.GetRequiredService<DashboardRepository>();
            var window = new DashboardWindow(repo) { Owner = this };
            window.ShowDialog();
        }

        // ============================================
        // BOTTOM BUTTONS
        // ============================================
        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            // Pass 'true' to indicate this is a manual refresh
            await LoadScriptsAsync(true);
        }

        private void btnCancelRun_Click(object sender, RoutedEventArgs e)
        {
            if (_runCts == null || _runCts.IsCancellationRequested) return;

            var confirm = MessageBox.Show(
                "Cancel the currently running query?\n\n" +
                "Any scripts that have already completed will keep their Excel files.\n" +
                "The currently-running script will be aborted.",
                "Cancel Run",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                _runCts.Cancel();
                AppendProgress("⚠ Cancel requested...");
            }
        }

        // ============================================
        // RUN
        // ============================================
        private async void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || _currentDepartment == null)
            {
                MessageBox.Show("You must be logged in to run scripts.",
                    "Not Logged In", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedScripts = lstScripts.SelectedItems.Cast<Script>().ToList();
            var outputFolder = txtOutputFolder.Text.Trim();
            var dateFrom = dtpFrom.SelectedDate;
            var dateTo = dtpTo.SelectedDate;

            var validator = new ScriptRunValidator();
            var validationResult = validator.Validate((selectedScripts, outputFolder, dateFrom, dateTo));

            if (!validationResult.IsValid)
            {
                var messages = string.Join("\n\n",
                    validationResult.Errors.Select(err => "• " + err.ErrorMessage));

                MessageBox.Show(
                    "Please fix the following before running:\n\n" + messages,
                    "Validation Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                var firstError = validationResult.Errors.First().PropertyName;
                switch (firstError)
                {
                    case "Scripts": lstScripts.Focus(); break;
                    case "OutputFolder": txtOutputFolder.Focus(); break;
                    case "DateFrom": dtpFrom.Focus(); break;
                    case "DateTo": dtpTo.Focus(); break;
                }
                return;
            }

            System.IO.Directory.CreateDirectory(outputFolder);

            var dateFromValue = dateFrom!.Value;
            var dateToValue = dateTo!.Value;

            btnRun.IsEnabled = false;
            btnRefresh.IsEnabled = false;
            btnCancelRun.IsEnabled = true;

            _runCts = new CancellationTokenSource();
            var token = _runCts.Token;

            prgProgress.IsIndeterminate = false;
            prgProgress.Minimum = 0;
            prgProgress.Maximum = 100;
            prgProgress.Value = 0;
            lblProgressPercent.Text = "0%";

            int total = selectedScripts.Count;
            int completed = 0;
            int succeeded = 0;
            int failed = 0;
            bool cancelled = false;

            try
            {
                foreach (var script in selectedScripts)
                {
                    if (token.IsCancellationRequested)
                    {
                        cancelled = true;
                        AppendProgress("✘ Run cancelled by user.");
                        break;
                    }

                    lblProgressStatus.Text = $"Running: {script.Name} ({completed + 1} of {total})";

                    bool ok = await RunOneScriptAsync(script, dateFromValue, dateToValue, outputFolder, token);
                    completed++;
                    if (ok) succeeded++; else failed++;

                    double newValue = (double)completed / total * 100.0;
                    await AnimateProgressBarAsync(prgProgress, newValue);
                    lblProgressPercent.Text = $"{newValue:0}%";
                }
            }
            finally
            {
                if (cancelled)
                    lblProgressStatus.Text = $"Cancelled. Completed {completed} of {total}.";
                else
                    lblProgressStatus.Text = $"Done. Succeeded: {succeeded}, Failed: {failed}.";

                lblProgressPercent.Text = cancelled ? $"{prgProgress.Value:0}%" : "100%";

                if (cancelled)
                    ToastService.Warning($"Run cancelled. {succeeded} of {total} succeeded.");
                else if (failed == 0)
                    ToastService.Success($"{succeeded} script(s) completed successfully.");
                else
                    ToastService.Warning($"{succeeded} succeeded, {failed} failed. Check the progress log.");

                await Task.Delay(5000);

                prgProgress.Value = 0;
                lblProgressPercent.Text = "";
                lblProgressStatus.Text = "";
                lblSelectedScript.Text = "(none)";
                lblSelectedScript.FontStyle = FontStyles.Italic;

                btnRun.IsEnabled = true;
                btnRefresh.IsEnabled = true;
                btnCancelRun.IsEnabled = false;

                _runCts?.Dispose();
                _runCts = null;
            }
        }

        private async Task AnimateProgressBarAsync(ProgressBar bar, double target)
        {
            double start = bar.Value;
            const int steps = 25;
            const int delayMs = 16;
            for (int i = 1; i <= steps; i++)
            {
                bar.Value = start + (target - start) * i / steps;
                await Task.Delay(delayMs);
            }
            bar.Value = target;
        }

        private async Task<bool> RunOneScriptAsync(
            Script script,
            DateTime dateFrom,
            DateTime dateTo,
            string outputFolder,
            CancellationToken token)
        {
            long runId = 0;
            AppendProgress($"▶ Starting: {script.Name}");

            try
            {
                runId = await _scriptRepository.StartScriptRunAsync(
                    _currentUser!.UserId, script.ScriptId, dateFrom, dateTo);

                var table = await KabarakScriptRunner.Core.Services.ScriptExecutorService.ExecuteScriptAsync(
                    App.ConnectionString,
                    script.SqlText,
                    script.NeedsDateFrom ? dateFrom : null,
                    script.NeedsDateTo ? dateTo : null,
                    token);

                AppendProgress($"   Query returned {table.Rows.Count} rows.");

                var filePath = KabarakScriptRunner.Core.Services.ExcelExportService.ExportToExcel(
                    table, outputFolder, script.Name);

                AppendProgress($"✔ Saved: {System.IO.Path.GetFileName(filePath)}");

                await _scriptRepository.FinishScriptRunSuccessAsync(runId, table.Rows.Count, filePath);

                await LogAuditAsync(_currentUser!.UserId, "ScriptRan", "Script",
                    script.ScriptId.ToString(),
                    $"Ran script '{script.Name}' (v{script.Version}). Rows={table.Rows.Count}. Output={System.IO.Path.GetFileName(filePath)}.");

                return true;
            }
            catch (OperationCanceledException)
            {
                AppendProgress($"✘ Cancelled: {script.Name}");

                if (runId > 0)
                {
                    try { await _scriptRepository.FinishScriptRunFailedAsync(runId, "Cancelled by user."); }
                    catch { }
                }

                await LogAuditAsync(_currentUser!.UserId, "ScriptRan", "Script",
                    script.ScriptId.ToString(),
                    $"Script '{script.Name}' was cancelled by the user.");

                return false;
            }
            catch (Exception ex)
            {
                AppendProgress($"✘ Failed: {ex.Message}");

                if (runId > 0)
                {
                    try { await _scriptRepository.FinishScriptRunFailedAsync(runId, ex.Message); }
                    catch { }
                }

                await LogAuditAsync(_currentUser!.UserId, "ScriptRan", "Script",
                    script.ScriptId.ToString(),
                    $"Script '{script.Name}' failed: {ex.Message}");

                MessageBox.Show($"Error running script '{script.Name}':\n\n{ex.Message}",
                    "Script Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void btnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || !_currentUser.IsAdmin) return;

            var window = new ChangePasswordWindow(_scriptRepository, _currentUser.UserId) { Owner = this };
            window.ShowDialog();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to close the application?",
                "Confirm Close",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
                Application.Current.Shutdown();
        }

        // ============================================
        // LOGOUT
        // ============================================
        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            if (LogoutRequested) return;

            var confirm = MessageBox.Show(
                $"Log out {_currentUser?.DisplayName ?? "this user"}?",
                "Confirm Logout",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            if (_currentUser != null)
            {
                _ = LogAuditAsync(_currentUser.UserId, "Logout", "User", _currentUser.UserId,
                    $"User '{_currentUser.UserId}' ({_currentUser.DisplayName}) logged out.");
            }

            LogoutRequested = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { Close(); }
                catch { }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}