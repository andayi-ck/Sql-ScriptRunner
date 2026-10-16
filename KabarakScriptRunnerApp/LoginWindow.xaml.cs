using System.Windows;
using System.Windows.Controls;
using KabarakScriptRunner.Data.Models;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunner.Core.Security;

namespace KabarakScriptRunnerApp
{
    public partial class LoginWindow : Window
    {
        private readonly ScriptRepository _scriptRepository;

        public User? AuthenticatedUser { get; private set; }
        public Department? SelectedDepartment { get; private set; }

        public LoginWindow(ScriptRepository scriptRepository)
        {
            InitializeComponent();
            _scriptRepository = scriptRepository;
            Loaded += LoginWindow_Loaded;
            txtUsername.Focus();
        }

        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var departments = await _scriptRepository.GetDepartmentsAsync();
                cmbDepartment.ItemsSource = departments;
                if (cmbDepartment.Items.Count > 0)
                    cmbDepartment.SelectedIndex = 0;
            }
            catch (System.Exception ex)
            {
                lblError.Text = $"Failed to load departments: {ex.Message}";
            }
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            lblError.Text = "";

            var username = txtUsername.Text.Trim();
            var password = txtPassword.Password;
            var selectedDept = cmbDepartment.SelectedItem as Department;

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                selectedDept == null)
            {
                lblError.Text = "Please enter username, password, and select a department.";
                return;
            }

            try
            {
                var user = await _scriptRepository.GetUserByIdAsync(username);

                // 1. User must exist
                if (user == null)
                {
                    lblError.Text = "Incorrect username, password, or department. Please try again.";
                    await LogAuditAsync(username, "LoginFailure", "User", username,
                        $"Login failed: user not found or inactive. Department attempted: {selectedDept.DepartmentName}");
                    return;
                }

                // 2. Password must match
                bool passwordOk = PasswordHasher.Verify(password, user.PasswordHash);
                if (!passwordOk)
                {
                    lblError.Text = "Incorrect username, password, or department. Please try again.";
                    await LogAuditAsync(username, "LoginFailure", "User", username,
                        $"Login failed: incorrect password. Department attempted: {selectedDept.DepartmentName}");
                    return;
                }

                // 3. Department must match (unless admin)
                if (!user.IsAdmin)
                {
                    if (user.DepartmentId == null || user.DepartmentId != selectedDept.DepartmentId)
                    {
                        lblError.Text = "Incorrect username, password, or department. Please try again.";
                        await LogAuditAsync(username, "LoginFailure", "User", username,
                            $"Login failed: department mismatch. User's department: {user.DepartmentName ?? "(none)"}. " +
                            $"Attempted: {selectedDept.DepartmentName}");
                        return;
                    }
                }

                // 4. Success
                AuthenticatedUser = user;
                SelectedDepartment = selectedDept;

                await LogAuditAsync(user.UserId, "LoginSuccess", "User", user.UserId,
                    $"Logged in from department '{selectedDept.DepartmentName}'. IsAdmin={user.IsAdmin}.");

                DialogResult = true;
                Close();
            }
            catch (System.Exception ex)
            {
                lblError.Text = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Writes an audit trail row. Swallows errors so auditing never blocks the user.
        /// </summary>
        private async Task LogAuditAsync(string userId, string actionType, string? targetType, string? targetId, string? details)
        {
            try
            {
                await _scriptRepository.LogAdminActionAsync(userId, actionType, targetType, targetId, details);
            }
            catch
            {
                // Never let an audit failure break login.
            }
        }


        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}