using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KabarakScriptRunner.Core.Security;
using KabarakScriptRunner.Data.Models;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunnerApp.Validation; // Ensure this matches your validator namespace

namespace KabarakScriptRunnerApp
{
    public partial class UserEditWindow : Window
    {
        private readonly ScriptRepository _scriptRepository;
        private readonly User? _existingUser;
        private readonly string _currentAdminUserId;

        private bool IsNewUser => _existingUser == null;

        public UserEditWindow(ScriptRepository scriptRepository, User? existingUser, string currentAdminUserId)
        {
            InitializeComponent();
            _scriptRepository = scriptRepository;
            _existingUser = existingUser;
            _currentAdminUserId = currentAdminUserId;

            Loaded += async (_, _) => await InitializeAsync();
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            try
            {
                var departments = await _scriptRepository.GetDepartmentsAsync();
                cmbDepartment.ItemsSource = departments;
            }
            catch (Exception ex)
            {
                lblError.Text = $"Failed to load departments: {ex.Message}";
                return;
            }

            if (IsNewUser)
            {
                lblHeader.Text = "Add New User";
                Title = "Add New User";
            }
            else
            {
                lblHeader.Text = $"Edit User: {_existingUser!.UserId}";
                Title = $"Edit User: {_existingUser.UserId}";

                txtUserId.Text = _existingUser.UserId;
                txtUserId.IsEnabled = false;

                txtDisplayName.Text = _existingUser.DisplayName;
                chkIsAdmin.IsChecked = _existingUser.IsAdmin;

                if (_existingUser.DepartmentId != null && cmbDepartment.ItemsSource != null)
                {
                    var list = cmbDepartment.ItemsSource.Cast<Department>().ToList();
                    cmbDepartment.SelectedItem = list.FirstOrDefault(d => d.DepartmentId == _existingUser.DepartmentId);
                }

                lblPassword.Visibility = Visibility.Collapsed;
                txtPassword.Visibility = Visibility.Collapsed;
                lblPasswordConfirm.Visibility = Visibility.Collapsed;
                txtPasswordConfirm.Visibility = Visibility.Collapsed;
            }
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            lblError.Text = "";

            var userId = txtUserId.Text.Trim();
            var displayName = txtDisplayName.Text.Trim();
            var dept = cmbDepartment.SelectedItem as Department;
            var isAdmin = chkIsAdmin.IsChecked == true;
            var password = txtPassword.Password;
            var confirm = txtPasswordConfirm.Password;

            // Execute pattern validation rule block
            var validator = new UserEditValidator(IsNewUser);
            var result = validator.Validate((userId, displayName, dept, password, confirm));

            if (!result.IsValid)
            {
                lblError.Text = string.Join("\n", result.Errors.Select(err => "• " + err.ErrorMessage));
                return;
            }

            if (IsNewUser)
            {
                try
                {
                    var hash = PasswordHasher.Hash(password);
                    await _scriptRepository.CreateUserAsync(userId, displayName, hash, isAdmin, dept!.DepartmentId);

                    await _scriptRepository.LogAdminActionAsync(
                        _currentAdminUserId,
                        "UserCreated",
                        "User",
                        userId,
                        $"Created new user '{userId}' ({displayName}) in department '{dept.DepartmentName}', IsAdmin={isAdmin}.");

                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    lblError.Text = $"Failed to create user: {ex.Message}";
                }
            }
            else
            {
                try
                {
                    await _scriptRepository.UpdateUserAsync(_existingUser!.UserId, displayName, isAdmin, dept!.DepartmentId);

                    await _scriptRepository.LogAdminActionAsync(
                        _currentAdminUserId,
                        "UserUpdated",
                        "User",
                        _existingUser.UserId,
                        $"Updated user '{_existingUser.UserId}': DisplayName='{displayName}', " +
                        $"Department='{dept.DepartmentName}', IsAdmin={isAdmin}.");

                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    lblError.Text = $"Failed to update user: {ex.Message}";
                }
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}