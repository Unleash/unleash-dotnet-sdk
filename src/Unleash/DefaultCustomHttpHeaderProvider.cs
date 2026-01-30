using System.Collections.Generic;

namespace Unleash
{
    internal class DefaultCustomHttpHeaderProvider : IUnleashCustomHttpHeaderProvider
    {
        public Dictionary<string, string> CustomHeaders
        {
            get
            {
                return new Dictionary<string, string>();
            }
        }
    }
}
