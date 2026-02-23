using NUnit.Framework;

[SetUpFixture]
public class GlobalTestSetup
{
    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        // Aggressively cached in Yggdrasil Engine, required for the hostname test to work
        Environment.SetEnvironmentVariable("hostname", "unit-test");
    }
}