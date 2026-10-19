using KabarakScriptRunner.Core.Security;
using Xunit;

namespace KabarakScriptRunner.Tests
{
    public class PasswordHasherTests
    {
        [Fact]
        public void Hash_ReturnsNonEmptyString()
        {
            var hash = PasswordHasher.Hash("TestPassword123");
            Assert.False(string.IsNullOrWhiteSpace(hash));
        }

        [Fact]
        public void Hash_ProducesDifferentHashesForSamePassword()
        {
            // BCrypt salts each hash, so hashing the same password twice
            // should produce different strings.
            var hash1 = PasswordHasher.Hash("TestPassword123");
            var hash2 = PasswordHasher.Hash("TestPassword123");
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void Verify_ReturnsTrueForCorrectPassword()
        {
            var password = "TestPassword123";
            var hash = PasswordHasher.Hash(password);
            Assert.True(PasswordHasher.Verify(password, hash));
        }

        [Fact]
        public void Verify_ReturnsFalseForWrongPassword()
        {
            var hash = PasswordHasher.Hash("CorrectPassword");
            Assert.False(PasswordHasher.Verify("WrongPassword", hash));
        }

        [Fact]
        public void Verify_ReturnsFalseForNullOrEmptyHash()
        {
            Assert.False(PasswordHasher.Verify("anything", ""));
            Assert.False(PasswordHasher.Verify("anything", null!));
        }
    }
}