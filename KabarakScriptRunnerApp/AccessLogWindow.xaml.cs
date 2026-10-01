using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KabarakScriptRunner.Core.Services;
using KabarakScriptRunner.Data.Repositories;

namespace KabarakScriptRunnerApp
{
    public partial class AccessLogWindow : Window
    {
        private readonly ScriptRepository _scriptRepository;

        public AccessLogWindow(ScriptRepository scriptRepository)
        {
            InitializeComponent();
            _scriptRepository = scriptRepository;

            Loaded += async (_, _) => await InitializeAsync();
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            try
            {
                var users = (await _scriptRepository.GetAllUsersAsync()).ToList();
                var placeholder = new KabarakScriptRunner.Data.Models.User
                {
                    UserId = "",
                    DisplayName = "(All Users)"
                };
                var list = new System.Collections.Generic.List<KabarakScriptRunner.Data.Models.User> { placeholder };
                list.AddRange(users);
                cmbUser.ItemsSource = list;
                cmbUser.SelectedIndex = 0;

                dtpFrom.SelectedDate = DateTime.Today.AddDays(-30);
                dtpTo.SelectedDate = DateTime.Today;

                await LoadActiveTabAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsScriptRunsTab => tabLogs.SelectedIndex == 0;

        private async System.Threading.Tasks.Task LoadActiveTabAsync()
        {
            if (IsScriptRunsTab)
                await LoadScriptRunsAsync();
            else
                await LoadAdminActionsAsync();
        }

        private async System.Threading.Tasks.Task LoadScriptRunsAsync()
        {
            try
            {
                var userId = (cmbUser.SelectedItem as KabarakScriptRunner.Data.Models.User)?.UserId;
                var status = (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (status == "(All)") status = null;

                var table = await _scriptRepository.GetScriptRunsAsync(
                    dtpFrom.SelectedDate, dtpTo.SelectedDate, userId, status);

                dgLogs.ItemsSource = table.DefaultView;
                lblRowCount.Text = $"{table.Rows.Count} script run(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load script runs:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadAdminActionsAsync()
        {
            try
            {
                var table = await _scriptRepository.GetAdminActionsAsync(
                    dtpFrom.SelectedDate, dtpTo.SelectedDate);

                dgActions.ItemsSource = table.DefaultView;
                lblRowCount.Text = $"{table.Rows.Count} admin action(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load admin actions:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void tabLogs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LoadActiveTabAsync();
        }

        private async void btnApply_Click(object sender, RoutedEventArgs e)
        {
            await LoadActiveTabAsync();
        }

        private async void btnClear_Click(object sender, RoutedEventArgs e)
        {
            dtpFrom.SelectedDate = DateTime.Today.AddDays(-30);
            dtpTo.SelectedDate = DateTime.Today;
            cmbUser.SelectedIndex = 0;
            cmbStatus.SelectedIndex = 0;
            await LoadActiveTabAsync();
        }

        private async void btnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DataTable table;
                string title;
                string subtitle;

                if (IsScriptRunsTab)
                {
                    var userId = (cmbUser.SelectedItem as KabarakScriptRunner.Data.Models.User)?.UserId;
                    var status = (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (status == "(All)") status = null;

                    table = await _scriptRepository.GetScriptRunsAsync(
                        dtpFrom.SelectedDate, dtpTo.SelectedDate, userId, status);

                    title = "Kabarak Script Runner — Script Runs";
                    subtitle = $"Filter: {dtpFrom.SelectedDate:yyyy-MM-dd} to {dtpTo.SelectedDate:yyyy-MM-dd}";
                    if (!string.IsNullOrEmpty(userId)) subtitle += $" • User: {userId}";
                    if (!string.IsNullOrEmpty(status)) subtitle += $" • Status: {status}";
                }
                else
                {
                    table = await _scriptRepository.GetAdminActionsAsync(
                        dtpFrom.SelectedDate, dtpTo.SelectedDate);

                    title = "Kabarak Script Runner — Admin Actions";
                    subtitle = $"Filter: {dtpFrom.SelectedDate:yyyy-MM-dd} to {dtpTo.SelectedDate:yyyy-MM-dd}";
                }

                if (table.Rows.Count == 0)
                {
                    MessageBox.Show("There are no rows to export.",
                        "Nothing to Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var path = PdfExportService.ExportDataTableToPdf(table, outputFolder, title, subtitle);

                MessageBox.Show($"PDF exported to:\n\n{path}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export PDF:\n\n{ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}