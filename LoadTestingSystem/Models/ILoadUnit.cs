namespace LoadTestingSytem.Models
{
    public interface ILoadUnit
    {
        Task RunAsync(string testName);
    }

    public class LoadUnitInvocation
    {
        public ILoadUnit LoadUnit { get; }

        public LoadUnitInvocation(ILoadUnit loadUnit)
        {
            LoadUnit = loadUnit;
        }
    }
}
