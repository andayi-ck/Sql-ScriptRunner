using System;
using System.Linq;
using System.Windows;
using KabarakScriptRunner.Core.Security;
using KabarakScriptRunner.Data.Models;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunnerApp.Validation; // Ensure this matches your validator namespace

namespace KabarakScriptRunnerApp
{
    public partial class ChangePasswordWindow : Window
    {
        private readonly ScriptRepository _scriptRepository;
        private readonly string _currentAdminUserId;

        public ChangePasswordWindow(ScriptRepository scriptRepository, string currentAdminUserId)
        {
            InitializeComponent();
            _scriptRepository = scriptRepository;
            _currentAdminUserId = currentAdminUserId;

            Loaded += async (_, _) => await LoadUsersAsync();
        }

        private async System.Threading.Tasks.Task LoadUsersAsync()
        {
            try
            {
                var users = await _scriptRepository.GetAllUsersAsync();
                cmbUser.ItemsSource = users;
            }
            catch (Exception ex)
            {
                lblError.Text = $"Failed to load users: {ex.Message}";
            }
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            lblError.Text = "";

            var user = cmbUser.SelectedItem as User;
            var newPwd = txtNew.Password;
            var confirm = txtConfirm.Password;

            // Execute pattern validation rule block
            var validator = new ChangePasswordValidator();
            var result = validator.Validate((user, newPwd, confirm));

            if (!result.IsValid)
            {
                lblError.Text = string.Join("\n", result.Errors.Select(err => "• " + err.ErrorMessage));
                return;
            }

            try
            {
                var hash = PasswordHasher.Hash(newPwd);
                await _scriptRepository.UpdateUserPasswordAsync(user!.UserId, hash);

                await _scriptRepository.LogAdminActionAsync(
                    _currentAdminUserId,
                    "PasswordChanged",
                    "User",
                    user.UserId,
                    $"Password reset for user '{user.UserId}' ({user.DisplayName}).");

                MessageBox.Show(
                    $"Password for user '{user.UserId}' ({user.DisplayName}) has been updated.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                lblError.Text = $"Failed to update password: {ex.Message}";
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}