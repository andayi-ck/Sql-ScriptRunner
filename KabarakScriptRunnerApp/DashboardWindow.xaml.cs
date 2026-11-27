using System;
using System.Windows;
using KabarakScriptRunner.Core.Services;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunnerApp.Services;
using KabarakScriptRunnerApp.ViewModels;

namespace KabarakScriptRunnerApp
{
    public partial class DashboardWindow : Window
    {
        private readonly DashboardViewModel _vm;

        private int _loadGeneration;

        public DashboardWindow(DashboardRepository dashboardRepository)
        {
            InitializeComponent();
            _vm = new DashboardViewModel(dashboardRepository);
            Loaded += async (_, _) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            int generation = ++_loadGeneration;

            try
            {
                lblStatus.Text = "Loading dashboard data...";

                KabarakScriptRunnerApp.Services.ToastService.Information("Please wait", "Querying information from the database...");

                await _vm.LoadAsync();

                if (generation != _loadGeneration) return;

                lblActiveStudents.Text = _vm.ActiveStudents.ToString("N0");
                lblActiveStaff.Text = _vm.ActiveStaff.ToString("N0");
                lblActiveScripts.Text = _vm.ActiveScripts.ToString("N0");
                lblFees30.Text = "KES " + _vm.FeesLast30DaysFormatted;
                lblRuns7.Text = _vm.RunsLast7Days.ToString("N0");

                chartFees.Series = _vm.FeesSeries;
                chartFees.XAxes = _vm.FeesXAxes;

                chartEnrolment.Series = _vm.EnrolmentSeries;
                chartEnrolment.XAxes = _vm.EnrolmentXAxes;

                chartCourses.Series = _vm.TopCoursesSeries;
                chartCourses.YAxes = _vm.TopCoursesYAxes;

                dgActivity.ItemsSource = _vm.RecentActivity;

                lblSubtitle.Text = $"Data as of {DateTime.Now:MMMM d, yyyy — HH:mm}";
                lblStatus.Text = "Dashboard loaded.";
            }
            catch (Exception ex)
            {
                if (generation != _loadGeneration) return;
                // EH-01: safe text only.
                var safe = SafeErrorMessage.For(ex, "DashboardLoad");
                lblStatus.Text = $"Failed to load dashboard: {safe}";
                MessageBox.Show($"Failed to load dashboard:\n\n{safe}",
                    "Dashboard Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private void btnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = new KabarakScriptRunner.Core.Services.DashboardSummaryData
                {
                    ActiveStudents = _vm.ActiveStudents,
                    ActiveStaff = _vm.ActiveStaff,
                    ActiveScripts = _vm.ActiveScripts,
                    TotalScripts = _vm.ActiveScripts,
                    FeesLast30Days = _vm.FeesLast30Days,
                    RunsLast7Days = _vm.RunsLast7Days
                };

                if (_vm.FeesByMonth != null)
                {
                    foreach (var item in _vm.FeesByMonth)
                        data.FeesByMonth.Add(new KabarakScriptRunner.Core.Services.MonthTotal
                        {
                            Period = item.Period,
                            Total = item.Total
                        });
                }

                if (_vm.StudentsByYear != null)
                {
                    foreach (var item in _vm.StudentsByYear)
                        data.StudentsByYear.Add(new KabarakScriptRunner.Core.Services.YearTotal
                        {
                            Year = item.Year,
                            Total = item.Total
                        });
                }

                if (_vm.RecentActivity != null)
                {
                    foreach (var item in _vm.RecentActivity)
                        data.RecentActivity.Add(new KabarakScriptRunner.Core.Services.ActivityRow
                        {
                            Timestamp = item.Timestamp,
                            UserId = item.UserId,
                            ActionType = item.ActionType,
                            Details = item.Details
                        });
                }

                var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var path = KabarakScriptRunner.Core.Services.PdfReportService.GenerateDashboardReport(folder, data);

                MessageBox.Show($"PDF exported to:\n\n{path}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch (Exception ex)
            {
                // EH-01: safe text only.
                var safe = SafeErrorMessage.For(ex, "DashboardExportPdf");
                MessageBox.Show($"Failed to export PDF:\n\n{safe}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}