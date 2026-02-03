using System;
using System.Threading.Tasks;
using Unleash.Strategies;

namespace Unleash.ClientFactory
{
    public interface IUnleashClientFactory
    {
        [Obsolete("This API will change in version 6. Details can be found in the v6_MIGRATION_GUIDE.md in the SDK repository.", false)]
        IUnleash CreateClient(UnleashSettings settings, bool synchronousInitialization = false, params IStrategy[] strategies);
        [Obsolete("This API will change in version 6. Details can be found in the v6_MIGRATION_GUIDE.md in the SDK repository.", false)]
        Task<IUnleash> CreateClientAsync(UnleashSettings settings, bool synchronousInitialization = false, params IStrategy[] strategies);
    }
}
