using FluentValidation;
using KabarakScriptRunner.Data.Models;

namespace KabarakScriptRunnerApp.Validation
{
    public class ChangePasswordValidator : AbstractValidator<(User? User, string NewPassword, string ConfirmPassword)>
    {
        public ChangePasswordValidator()
        {
            RuleFor(x => x.User)
                .NotNull().WithMessage("Please select a user.");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("Please enter a new password.")
                .MinimumLength(6).WithMessage("Password must be at least 6 characters.");

            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.NewPassword).WithMessage("The two passwords do not match.");
        }
    }
}