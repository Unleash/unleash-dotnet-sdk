using System;
using System.Threading.Tasks;
using Unleash.Internal;
using Unleash.Strategies;

namespace Unleash.ClientFactory
{
    public interface IUnleashClientFactory
    {
        IUnleash CreateClient(UnleashSettings settings, bool synchronousInitialization = false, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies);
        Task<IUnleash> CreateClientAsync(UnleashSettings settings, bool synchronousInitialization = false, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies);
    }
}
