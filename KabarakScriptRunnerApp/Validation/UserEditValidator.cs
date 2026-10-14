using FluentValidation;
using KabarakScriptRunner.Data.Models;

namespace KabarakScriptRunnerApp.Validation
{
    public class UserEditValidator : AbstractValidator<(string UserId, string DisplayName, Department? Department, string Password, string ConfirmPassword)>
    {
        private readonly bool _isNewUser;

        public UserEditValidator(bool isNewUser)
        {
            _isNewUser = isNewUser;

            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("User ID is required.")
                .MaximumLength(50).WithMessage("User ID must be 50 characters or fewer.");

            RuleFor(x => x.DisplayName)
                .NotEmpty().WithMessage("Display Name is required.")
                .MaximumLength(150).WithMessage("Display Name must be 150 characters or fewer.");

            RuleFor(x => x.Department)
                .NotNull().WithMessage("Please select a department.");

            // Password is required only when creating a new user.
            // When editing an existing user, the password fields are hidden and
            // resets are handled through the dedicated Change Password dialog.
            When(x => _isNewUser, () =>
            {
                RuleFor(x => x.Password)
                    .NotEmpty().WithMessage("Password is required for new users.")
                    .MinimumLength(6).WithMessage("Password must be at least 6 characters long.");

                RuleFor(x => x.ConfirmPassword)
                    .Equal(x => x.Password).WithMessage("The two passwords do not match.");
            });
        }
    }
}