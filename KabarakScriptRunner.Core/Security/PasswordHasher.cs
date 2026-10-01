using BCrypt.Net;

namespace KabarakScriptRunner.Core.Security
{
    public static class PasswordHasher
    {
        public static string Hash(string plainPassword)
        {
            return BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 11);
        }

        public static bool Verify(string plainPassword, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(plainPassword, storedHash);
            }
            catch
            {
                return false;
            }
        }
    }
}