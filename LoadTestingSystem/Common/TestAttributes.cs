namespace LoadTestingSytem.Common
{
    [AttributeUsage(AttributeTargets.Method)]
    public class TestPreparationAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class TestExecuteAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class TestResultValidatationAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class TestTokenExchangeAttribute : Attribute { }
}
