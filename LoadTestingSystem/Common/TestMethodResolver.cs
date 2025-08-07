using System.Reflection;

namespace LoadTestingSytem.Common
{
    public class TestMethodExecution
    {
        public MethodInfo InitMethod { get; }
        public MethodInfo ExecuteMethod { get; }
        public MethodInfo ValidateMethod { get; }
        public MethodInfo TokenExchangeMethod { get; }

        public TestMethodExecution(Type testClassType)
        {
            InitMethod = testClassType.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<TestPreparationAttribute>() != null)
                ?? throw new Exception("Missing method with [TestPreparation] attribute");

            ExecuteMethod = testClassType.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<TestExecuteAttribute>() != null)
                ?? throw new Exception("Missing method with [TestExecute] attribute");

            ValidateMethod = testClassType.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<TestResultValidatationAttribute>() != null)
                ?? throw new Exception("Missing method with [TestResultValidatation] attribute");

            TokenExchangeMethod = testClassType.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<TestTokenExchangeAttribute>() != null)
                ?? throw new Exception("Missing method with [TestTokenExchange] attribute");
        }
    }
}
