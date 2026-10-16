using System;
using System.Windows;
using KabarakScriptRunner.Core.Services;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunnerApp.ViewModels;

namespace KabarakScriptRunnerApp
{
    public partial class DashboardWindow : Window
    {
        private readonly DashboardViewModel _vm;

        public DashboardWindow(DashboardRepository dashboardRepository)
        {
            InitializeComponent();
            _vm = new DashboardViewModel(dashboardRepository);
            Loaded += async (_, _) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                lblStatus.Text = "Loading dashboard data...";

                await _vm.LoadAsync();

                // KPIs
                lblActiveStudents.Text = _vm.ActiveStudents.ToString("N0");
                lblActiveStaff.Text = _vm.ActiveStaff.ToString("N0");
                lblActiveScripts.Text = _vm.ActiveScripts.ToString("N0");
                lblFees30.Text = "KES " + _vm.FeesLast30DaysFormatted;
                lblRuns7.Text = _vm.RunsLast7Days.ToString("N0");

                // Charts
                chartFees.Series = _vm.FeesSeries;
                chartFees.XAxes = _vm.FeesXAxes;

                chartEnrolment.Series = _vm.EnrolmentSeries;
                chartEnrolment.XAxes = _vm.EnrolmentXAxes;

                chartCourses.Series = _vm.TopCoursesSeries;
                chartCourses.YAxes = _vm.TopCoursesYAxes;

                // Activity grid
                dgActivity.ItemsSource = _vm.RecentActivity;

                lblSubtitle.Text = $"Data as of {DateTime.Now:MMMM d, yyyy — HH:mm}";
                lblStatus.Text = "Dashboard loaded.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Failed to load dashboard: {ex.Message}";
                MessageBox.Show($"Failed to load dashboard:\n\n{ex.Message}",
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
                    TotalScripts = _vm.ActiveScripts,   // reuse; adjust if you track total separately
                    FeesLast30Days = _vm.FeesLast30Days,
                    RunsLast7Days = _vm.RunsLast7Days
                };

                // Populate fees by month from the loaded chart data
                if (_vm.FeesByMonth != null)
                {
                    foreach (var item in _vm.FeesByMonth)
                        data.FeesByMonth.Add(new KabarakScriptRunner.Core.Services.MonthTotal
                        {
                            Period = item.Period,
                            Total = item.Total
                        });
                }

                // Populate enrolments by year
                if (_vm.StudentsByYear != null)
                {
                    foreach (var item in _vm.StudentsByYear)
                        data.StudentsByYear.Add(new KabarakScriptRunner.Core.Services.YearTotal
                        {
                            Year = item.Year,
                            Total = item.Total
                        });
                }

                // Populate recent activity
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