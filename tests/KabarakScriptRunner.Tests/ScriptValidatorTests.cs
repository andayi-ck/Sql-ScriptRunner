using KabarakScriptRunner.Core.Services;
using Xunit;

namespace KabarakScriptRunner.Tests
{
    public class ScriptValidatorTests
    {
        [Fact]
        public void Validate_AcceptsSimpleSelect()
        {
            var sql = "SELECT * FROM uni.Students";
            var result = ScriptValidator.Validate(sql);
            Assert.True(result.IsValid, string.Join("; ", result.Errors));
        }

        [Fact]
        public void Validate_AcceptsSelectWithParameters()
        {
            var sql = "SELECT * FROM uni.Payments WHERE PaymentDate BETWEEN @DateFrom AND @DateTo";
            var result = ScriptValidator.Validate(sql);
            Assert.True(result.IsValid, string.Join("; ", result.Errors));
        }

        [Fact]
        public void Validate_RejectsEmptyScript()
        {
            var result = ScriptValidator.Validate("");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_RejectsDelete()
        {
            var result = ScriptValidator.Validate("DELETE FROM uni.Students WHERE StudentId = 1");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_RejectsDrop()
        {
            var result = ScriptValidator.Validate("DROP TABLE uni.Students");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_RejectsMultipleStatements()
        {
            var sql = "SELECT * FROM uni.Students; DROP TABLE uni.Students;";
            var result = ScriptValidator.Validate(sql);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_IgnoresKeywordsInsideStringLiterals()
        {
            // The word DELETE is inside a string literal - should NOT be flagged.
            var sql = "SELECT Name FROM uni.Courses WHERE Description LIKE '%DELETE%'";
            var result = ScriptValidator.Validate(sql);
            Assert.True(result.IsValid, string.Join("; ", result.Errors));
        }

        [Fact]
        public void Validate_RejectsUnsupportedParameter()
        {
            var sql = "SELECT * FROM uni.Students WHERE EnrolmentDate >= @CustomDate";
            var result = ScriptValidator.Validate(sql);
            Assert.False(result.IsValid);
        }
    }
}