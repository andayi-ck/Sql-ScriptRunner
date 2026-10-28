using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KabarakScriptRunner.Core.Services;
using KabarakScriptRunner.Data.Repositories;

namespace KabarakScriptRunnerApp
{
    public partial class AccessLogWindow : Window
    {
        private readonly ScriptRepository _scriptRepository;
        private readonly DashboardRepository _dashboardRepository;

        public AccessLogWindow(ScriptRepository scriptRepository, DashboardRepository dashboardRepository)
        {
            InitializeComponent();
            _scriptRepository = scriptRepository;
            _dashboardRepository = dashboardRepository;
            Loaded += async (_, _) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var users = (await _scriptRepository.GetAllUsersAsync()).ToList();
                var placeholder = new KabarakScriptRunner.Data.Models.User { UserId = "", DisplayName = "(All Users)" };
                var list = new System.Collections.Generic.List<KabarakScriptRunner.Data.Models.User> { placeholder };
                list.AddRange(users);
                cmbUser.ItemsSource = list;
                cmbUser.SelectedIndex = 0;

                dtpFrom.SelectedDate = DateTime.Today.AddDays(-30);
                dtpTo.SelectedDate = DateTime.Today;

                await LoadScriptRunsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadActiveTabAsync()
        {
            if (tabLogs.SelectedIndex == 0)
                await LoadScriptRunsAsync();
            else if (tabLogs.SelectedIndex == 1)
                await LoadAdminActionsAsync();
            else
                await LoadAnalyticsAsync();
        }

        private async Task LoadScriptRunsAsync()
        {
            try
            {
                var userId = (cmbUser.SelectedItem as KabarakScriptRunner.Data.Models.User)?.UserId;
                var status = (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (status == "(All)") status = null;

                var table = await _scriptRepository.GetScriptRunsAsync(dtpFrom.SelectedDate, dtpTo.SelectedDate, userId, status);

                dgLogs.ItemsSource = table.DefaultView;
                lblRowCount.Text = $"{table.Rows.Count} script run(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load script runs:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadAdminActionsAsync()
        {
            try
            {
                var table = await _scriptRepository.GetAdminActionsAsync(dtpFrom.SelectedDate, dtpTo.SelectedDate);
                var selected = (cmbActionType?.SelectedItem as ComboBoxItem)?.Content?.ToString();

                if (selected != null && selected != "(All)" && table.Rows.Count > 0)
                {
                    var filtered = table.Clone();
                    foreach (DataRow row in table.Rows)
                    {
                        if (row["ActionType"]?.ToString() == selected) filtered.ImportRow(row);
                    }
                    dgActions.ItemsSource = filtered.DefaultView;
                    lblRowCount.Text = $"{filtered.Rows.Count} of {table.Rows.Count} admin action(s)";
                }
                else
                {
                    dgActions.ItemsSource = table.DefaultView;
                    lblRowCount.Text = $"{table.Rows.Count} admin action(s)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load admin actions:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadAnalyticsAsync()
        {
            try
            {
                var loginsPerDay = (await _dashboardRepository.GetLoginsPerDayAsync(30)).ToList();
                chartLogins.Series = new LiveChartsCore.ISeries[]
                {
                    new LiveChartsCore.SkiaSharpView.LineSeries<int>
                    {
                        Name = "Logins",
                        Values = loginsPerDay.Select(x => x.Total).Select(Convert.ToInt32).ToArray(),
                        Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SkiaSharp.SKColor.Parse("#1565C0")) { StrokeThickness = 3 },
                        Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SkiaSharp.SKColor.Parse("#4FC3F7").WithAlpha(60)),
                        GeometrySize = 8
                    }
                };
                chartLogins.XAxes = new[] { new LiveChartsCore.SkiaSharpView.Axis { Labels = loginsPerDay.Select(x => x.Period).ToArray(), LabelsRotation = 45, TextSize = 10 } };

                var stats = await _dashboardRepository.GetLoginStatsAsync(30);
                chartLoginPie.Series = new LiveChartsCore.ISeries[]
                {
                    new LiveChartsCore.SkiaSharpView.PieSeries<int>
                    {
                        Values = new[] { stats.Success, stats.Failed },
                        Name = "Logins",
                        DataLabelsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SkiaSharp.SKColors.White),
                        DataLabelsSize = 12,
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                        DataLabelsFormatter = point => point.Coordinate.PrimaryValue.ToString()
                    }
                };
                chartLoginPie.LegendPosition = LiveChartsCore.Measure.LegendPosition.Right;
                chartLoginPie.LegendTextPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SkiaSharp.SKColors.Black);
                chartLoginPie.LegendTextSize = 11;

                var topScripts = (await _dashboardRepository.GetTopScriptsByRunsAsync(10)).ToList();
                chartTopScripts.Series = new LiveChartsCore.ISeries[]
                {
                    new LiveChartsCore.SkiaSharpView.RowSeries<int>
                    {
                        Name = "Runs",
                        Values = topScripts.Select(x => x.Total).ToArray(),
                        Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SkiaSharp.SKColor.Parse("#4FC3F7")),
                        Stroke = null,
                        DataLabelsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SkiaSharp.SKColors.Black),
                        DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                        DataLabelsSize = 10
                    }
                };
                chartTopScripts.YAxes = new[] { new LiveChartsCore.SkiaSharpView.Axis { Labels = topScripts.Select(x => x.Label).ToArray(), TextSize = 10 } };

                var actionsByUser = (await _dashboardRepository.GetActionsByUserAsync(30)).ToList();
                chartActionsByUser.Series = new LiveChartsCore.ISeries[]
                {
                    new LiveChartsCore.SkiaSharpView.RowSeries<int>
                    {
                        Name = "Actions",
                        Values = actionsByUser.Select(x => x.Total).ToArray(),
                        Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SkiaSharp.SKColor.Parse("#7EC8F2")),
                        Stroke = null
                    }
                };
                chartActionsByUser.YAxes = new[] { new LiveChartsCore.SkiaSharpView.Axis { Labels = actionsByUser.Select(x => x.Label).ToArray(), TextSize = 10 } };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load analytics:\n\n{ex.Message}", "Analytics Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void tabLogs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LoadActiveTabAsync();
        }

        private async void cmbActionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LoadAdminActionsAsync();
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
            if (cmbActionType != null) cmbActionType.SelectedIndex = 0;
            await LoadActiveTabAsync();
        }

        private async void btnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            // ... (Your existing PDF export code remains the same) ...
            try
            {
                DataTable table;
                string title;
                string subtitle;

                if (tabLogs.SelectedIndex == 0) // Script Runs
                {
                    var userId = (cmbUser.SelectedItem as KabarakScriptRunner.Data.Models.User)?.UserId;
                    var status = (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (status == "(All)") status = null;
                    table = await _scriptRepository.GetScriptRunsAsync(dtpFrom.SelectedDate, dtpTo.SelectedDate, userId, status);
                    title = "Kabarak Script Runner — Script Runs";
                    subtitle = $"Filter: {dtpFrom.SelectedDate:yyyy-MM-dd} to {dtpTo.SelectedDate:yyyy-MM-dd}";
                }
                else // Admin Actions
                {
                    table = await _scriptRepository.GetAdminActionsAsync(dtpFrom.SelectedDate, dtpTo.SelectedDate);
                    var selected = (cmbActionType?.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (selected != null && selected != "(All)" && table.Rows.Count > 0)
                    {
                        var filtered = table.Clone();
                        foreach (DataRow row in table.Rows)
                        {
                            if (row["ActionType"]?.ToString() == selected) filtered.ImportRow(row);
                        }
                        table = filtered;
                    }
                    title = "Kabarak Script Runner — Admin Actions";
                    subtitle = $"Filter: {dtpFrom.SelectedDate:yyyy-MM-dd} to {dtpTo.SelectedDate:yyyy-MM-dd}";
                }

                if (table.Rows.Count == 0) { MessageBox.Show("No rows to export."); return; }

                string outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var path = PdfReportService.GenerateDataReport(table, outputFolder, title, subtitle, generatedBy: "Administrator");
                MessageBox.Show($"PDF exported to:\n\n{path}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export PDF:\n\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}