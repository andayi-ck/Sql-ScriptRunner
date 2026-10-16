using System;
using System.Windows;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunnerApp.Services;
using Microsoft.Win32;

namespace KabarakScriptRunnerApp
{
    public partial class OptionsWindow : Window
    {
        private readonly ScriptRepository _scriptRepository;

        public OptionsWindow(ScriptRepository scriptRepository)
        {
            InitializeComponent();
            _scriptRepository = scriptRepository;

            Loaded += async (_, _) => await LoadOptionsAsync();
        }

        private async System.Threading.Tasks.Task LoadOptionsAsync()
        {
            try
            {
                txtOutputFolder.Text = await _scriptRepository.GetSettingAsync("OutputFolder") ?? "";
                txtAppTitle.Text = await _scriptRepository.GetSettingAsync("AppTitle") ?? "";
            }
            catch (Exception ex)
            {
                lblStatus.Foreground = System.Windows.Media.Brushes.Red;
                lblStatus.Text = $"Failed to load options: {ex.Message}";
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select default output folder",
                InitialDirectory = System.IO.Directory.Exists(txtOutputFolder.Text)
                    ? txtOutputFolder.Text
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
                txtOutputFolder.Text = dialog.FolderName;
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _scriptRepository.SetSettingAsync("OutputFolder", txtOutputFolder.Text.Trim());
                await _scriptRepository.SetSettingAsync("AppTitle", txtAppTitle.Text.Trim());

                lblStatus.Foreground = System.Windows.Media.Brushes.Green;
                lblStatus.Text = "Options saved successfully.";
                ToastService.Success("Options saved.");
            }
            catch (Exception ex)
            {
                lblStatus.Foreground = System.Windows.Media.Brushes.Red;
                lblStatus.Text = $"Failed to save options: {ex.Message}";
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnViewLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KabarakScriptRunner", "logs");

                if (!System.IO.Directory.Exists(logDir))
                    System.IO.Directory.CreateDirectory(logDir);

                System.Diagnostics.Process.Start("explorer.exe", logDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open the logs folder:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}