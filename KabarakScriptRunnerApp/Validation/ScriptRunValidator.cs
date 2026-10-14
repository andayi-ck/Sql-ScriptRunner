using System;
using System.Collections.Generic;
using FluentValidation;
using KabarakScriptRunner.Data.Models;

namespace KabarakScriptRunnerApp.Validation
{
    public class ScriptRunValidator : AbstractValidator<(List<Script> Scripts, string OutputFolder, DateTime? DateFrom, DateTime? DateTo)>
    {
        public ScriptRunValidator()
        {
            RuleFor(x => x.Scripts)
                .NotEmpty().WithMessage("Please select at least one script before running.");

            RuleFor(x => x.OutputFolder)
                .NotEmpty().WithMessage("Please select an output folder.")
                .Must(DirectoryIsValid).WithMessage("The output folder is not accessible.");

            RuleFor(x => x.DateFrom)
                .NotNull().WithMessage("Please select a 'Date From'.");

            RuleFor(x => x.DateTo)
                .NotNull().WithMessage("Please select a 'Date To'.")
                .Must((x, dateTo) => dateTo <= DateTime.Today)
                .WithMessage("The 'Date To' cannot be in the future.")
                .Must((x, dateTo) => !x.DateFrom.HasValue || dateTo >= x.DateFrom)
                .WithMessage("The 'Date To' cannot be earlier than the 'Date From'.");
        }

        private static bool DirectoryIsValid(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                System.IO.Directory.CreateDirectory(path);
                return true;
            }
            catch { return false; }
        }
    }
}