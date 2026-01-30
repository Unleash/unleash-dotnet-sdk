using Unleash.Internal;
using Unleash.Tests.Mock;
using System.Text;

namespace Unleash.Tests
{
    public class MockedUnleashSettings : UnleashSettings
    {
        public MockedUnleashSettings(bool mockFileSystem = true, string instanceTag = "test instance 1")
        {
            AppName = "test";
            InstanceTag = instanceTag;
            UnleashApi = new Uri("http://localhost:4242/");
            DisableSingletonWarning = true;

            UnleashApiClient = new MockApiClient();
            FileSystem = new MockFileSystem();

            if (!mockFileSystem)
            {
                FileSystem = new FileSystem(Encoding.UTF8);
            }

            UnleashContextProvider = new DefaultUnleashContextProvider(new UnleashContext
            {
                UserId = "userA",
                SessionId = "sessionId",
                RemoteAddress = "remoteAddress",
                Properties = new Dictionary<string, string>()
            });
        }
    }
}