using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using KabarakScriptRunner.Data.Repositories;

namespace KabarakScriptRunnerApp.ViewModels
{
    public class DashboardViewModel
    {
        private readonly DashboardRepository _repository;

        public DashboardViewModel(DashboardRepository repository)
        {
            _repository = repository;
        }

        // ============================================
        // KPI SUMMARY
        // ============================================
        public int ActiveStudents { get; private set; }
        public int ActiveStaff { get; private set; }
        public int ActiveScripts { get; private set; }
        public decimal FeesLast30Days { get; private set; }
        public int RunsLast7Days { get; private set; }

        public List<MonthlyTotal> FeesByMonth { get; private set; } = new();
        public List<YearlyTotal> StudentsByYear { get; private set; } = new();


        // ============================================
        // CHART SERIES
        // ============================================
        public ISeries[] FeesSeries { get; private set; } = Array.Empty<ISeries>();
        public Axis[] FeesXAxes { get; private set; } = Array.Empty<Axis>();

        public ISeries[] EnrolmentSeries { get; private set; } = Array.Empty<ISeries>();
        public Axis[] EnrolmentXAxes { get; private set; } = Array.Empty<Axis>();

        public ISeries[] TopCoursesSeries { get; private set; } = Array.Empty<ISeries>();
        public Axis[] TopCoursesYAxes { get; private set; } = Array.Empty<Axis>();

        // ============================================
        // RECENT ACTIVITY
        // ============================================
        public List<RecentActivity> RecentActivity { get; private set; } = new();

        // ============================================
        // LOAD
        // ============================================
        public async Task LoadAsync()
        {
            var summary = await _repository.GetSummaryAsync();
            ActiveStudents = summary.ActiveStudents;
            ActiveStaff = summary.ActiveStaff;
            ActiveScripts = summary.ActiveScripts;
            FeesLast30Days = summary.FeesLast30Days;
            RunsLast7Days = summary.RunsLast7Days;

            // 1. Fees collected — bar chart
            var fees = (await _repository.GetMonthlyFeesCollectedAsync(12)).ToList();
            FeesByMonth = fees;
            FeesSeries = new ISeries[]
            {
                new ColumnSeries<decimal>
                {
                    Name = "Fees Collected",
                    Values = fees.Select(f => f.Total).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#4FC3F7")),
                    Stroke = null,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsSize = 11
                }
            };
            FeesXAxes = new[]
            {
                new Axis
                {
                    Labels = fees.Select(f => f.Period).ToArray(),
                    LabelsRotation = 45,
                    TextSize = 11
                }
            };

            // 2. Enrolment — line chart
            var enrol = (await _repository.GetStudentsAdmittedPerYearAsync(5)).ToList();
            StudentsByYear = enrol;
            EnrolmentSeries = new ISeries[]
            {
                new LineSeries<int>
                {
                    Name = "Students Admitted",
                    Values = enrol.Select(e => e.Total).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#4FC3F7").WithAlpha(50)),
                    Stroke = new SolidColorPaint(SKColor.Parse("#1565C0")) { StrokeThickness = 3 },
                    GeometrySize = 10,
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#1565C0")) { StrokeThickness = 3 },
                    GeometryFill = new SolidColorPaint(SKColors.White)
                }
            };
            EnrolmentXAxes = new[]
            {
                new Axis
                {
                    Labels = enrol.Select(e => e.Year.ToString()).ToArray(),
                    TextSize = 12
                }
            };

            // 3. Top courses — horizontal bar
            var courses = (await _repository.GetTopCoursesByEnrolmentAsync(10)).ToList();
            TopCoursesSeries = new ISeries[]
            {
                new RowSeries<int>
                {
                    Name = "Enrolments",
                    Values = courses.Select(c => c.Total).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#7EC8F2")),
                    Stroke = null,
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                    DataLabelsSize = 11
                }
            };
            TopCoursesYAxes = new[]
            {
                new Axis
                {
                    Labels = courses.Select(c => c.Label).ToArray(),
                    TextSize = 10
                }
            };

            // 4. Recent activity
            RecentActivity = (await _repository.GetRecentActivityAsync(20)).ToList();
        }

        // ============================================
        // HELPERS FOR UI BINDING
        // ============================================
        public string FeesLast30DaysFormatted => FeesLast30Days.ToString("N0");
    }
}