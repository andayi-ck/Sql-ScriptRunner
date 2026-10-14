using KabarakScriptRunner.Data.Models;

namespace KabarakScriptRunnerApp.Services
{
    public interface ICurrentUserService
    {
        User? User { get; }
        Department? Department { get; }
        void Set(User user, Department department);
        void Clear();
    }

    public class CurrentUserService : ICurrentUserService
    {
        public User? User { get; private set; }
        public Department? Department { get; private set; }

        public void Set(User user, Department department)
        {
            User = user;
            Department = department;
        }

        public void Clear()
        {
            User = null;
            Department = null;
        }
    }
}