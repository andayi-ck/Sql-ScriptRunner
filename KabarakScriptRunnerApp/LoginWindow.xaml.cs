using System;
using System.Windows;
using System.Windows.Controls;
using KabarakScriptRunner.Data.Models;
using KabarakScriptRunner.Data.Repositories;
using KabarakScriptRunner.Core.Security;
using Serilog; // Make sure you have Serilog imported for F-07 error logging

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
            catch (Exception ex)
            {
                // F-07 Fix: Log the real error, show generic
                Log.Error(ex, "Failed to load departments on login screen");
                lblError.Text = "Failed to load departments. Please check your connection or contact support.";
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

                // 2. Check if currently locked out (F-05/F-06)
                if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.Now)
                {
                    var remaining = Math.Ceiling((user.LockoutEnd.Value - DateTime.Now).TotalMinutes);
                    lblError.Text = $"Account is locked. Please try again in {remaining} minute(s).";
                    return;
                }

                // 3. Password must match
                bool passwordOk = PasswordHasher.Verify(password, user.PasswordHash);
                if (!passwordOk)
                {
                    await _scriptRepository.IncrementFailedAttemptsAsync(user.UserId, user.FailedAttempts);

                    if (user.FailedAttempts + 1 >= 5)
                    {
                        lblError.Text = "Account locked for 5 minutes due to too many failed attempts.";
                        await LogAuditAsync(username, "AccountLocked", "User", username, "Locked out for 5 minutes.");
                    }
                    else
                    {
                        lblError.Text = "Incorrect username, password, or department. Please try again.";
                        await LogAuditAsync(username, "LoginFailure", "User", username,
                            $"Login failed: incorrect password. Department attempted: {selectedDept.DepartmentName}");
                    }
                    return;
                }

                // 4. Department must match (unless admin)
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

                // 5. Success: Reset failed attempts
                await _scriptRepository.ResetFailedAttemptsAsync(user.UserId);

                AuthenticatedUser = user;
                SelectedDepartment = selectedDept;

                await LogAuditAsync(user.UserId, "LoginSuccess", "User", user.UserId,
                    $"Logged in from department '{selectedDept.DepartmentName}'. IsAdmin={user.IsAdmin}.");

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                // F-07 Fix: Log the real error, show generic
                Log.Error(ex, "Unexpected error during login");
                lblError.Text = "An unexpected error occurred. Please try again later.";
            }
        }

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