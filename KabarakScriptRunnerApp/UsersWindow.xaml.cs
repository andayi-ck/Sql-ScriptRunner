using System;
using System.Linq;
using System.Windows;
using KabarakScriptRunner.Data.Models;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunnerApp.Services;

namespace KabarakScriptRunnerApp
{
    public partial class UsersWindow : Window
    {
        private readonly ScriptRepository _scriptRepository;
        private readonly User _currentAdmin;

        public UsersWindow(ScriptRepository scriptRepository, User currentAdmin)
        {
            InitializeComponent();
            _scriptRepository = scriptRepository;
            _currentAdmin = currentAdmin;

            Loaded += async (_, _) => await LoadUsersAsync();
        }

        private async System.Threading.Tasks.Task LoadUsersAsync()
        {
            try
            {
                var users = await _scriptRepository.GetAllUsersAsync();
                dgUsers.ItemsSource = users.OrderBy(u => u.UserId).ToList();
            }
            catch (Exception ex)
            {
                // EH-01: safe text only.
                var safe = SafeErrorMessage.For(ex, "LoadUsers");
                MessageBox.Show($"Failed to load users:\n\n{safe}",
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadUsersAsync();
            ToastService.Information("User list refreshed.");
        }

        private async void btnNew_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new UserEditWindow(_scriptRepository, null, _currentAdmin.UserId)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                ToastService.Success("User created successfully.");
                await LoadUsersAsync();
            }
        }

        private async void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsers.SelectedItem is not UserSummary selected) return;

            var fullUser = await _scriptRepository.GetUserByIdAsync(selected.UserId);
            if (fullUser == null) return;

            var dlg = new UserEditWindow(_scriptRepository, fullUser, _currentAdmin.UserId) { Owner = this };
            if (dlg.ShowDialog() == true) await LoadUsersAsync();
        }

        private async void btnToggle_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsers.SelectedItem is not UserSummary selected)
            {
                MessageBox.Show("Please select a user to activate or deactivate.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selected.UserId == _currentAdmin.UserId)
            {
                MessageBox.Show("You cannot deactivate your own account.",
                    "Not Allowed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string action = selected.IsActive ? "deactivate" : "activate";
            var confirm = MessageBox.Show(
                $"Are you sure you want to {action} user '{selected.UserId}' ({selected.DisplayName})?",
                "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                bool newState = !selected.IsActive;
                await _scriptRepository.SetUserActiveAsync(selected.UserId, newState);

                await _scriptRepository.LogAdminActionAsync(
                    _currentAdmin.UserId,
                    newState ? "UserActivated" : "UserDeactivated",
                    "User",
                    selected.UserId,
                    $"{(newState ? "Activated" : "Deactivated")} user '{selected.UserId}' ({selected.DisplayName}).");

                ToastService.Success($"User '{selected.UserId}' {(newState ? "activated" : "deactivated")}.");

                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                // EH-01: safe text only.
                var safe = SafeErrorMessage.For(ex, "ToggleUserActive");
                MessageBox.Show($"Failed to update user:\n\n{safe}",
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}