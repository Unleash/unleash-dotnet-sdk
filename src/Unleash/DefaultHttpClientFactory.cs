using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Unleash
{
    /// <inheritdoc />
    public class DefaultHttpClientFactory : IHttpClientFactory
    {
        /// <summary>
        /// Default: 5 seconds
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Default: 60 seconds
        /// </summary>
        public TimeSpan ServicePointConnectionLeaseTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Default: null
        /// </summary>
        public Dictionary<string, string> CustomDefaultHttpHeaders { get; set; }

        /// <summary>
        /// Default: empty dictionary
        /// </summary>
        private static readonly ConcurrentDictionary<string, HttpClient> _httpClientCache = new ConcurrentDictionary<string, HttpClient>();

        public HttpClient Create(Uri unleashApiUri)
        {
            var key = $"{unleashApiUri.Scheme}://{unleashApiUri.DnsSafeHost}:{unleashApiUri.Port}";

            return _httpClientCache.GetOrAdd(key, k =>
            {
                var client = CreateHttpClientInstance(unleashApiUri);
                // Refresh DNS cache each 60 seconds
                var servicePoint = ServicePointManager.FindServicePoint(unleashApiUri);
                ConfigureServicePoint(servicePoint);
                ConfigureHttpClient(client);
                ConfigureDefaultRequestHeaders(client.DefaultRequestHeaders);

                return client;
            });
        }

        protected virtual HttpClient CreateHttpClientInstance(Uri unleashApiUri)
        {
            var client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            {
                BaseAddress = unleashApiUri,
                Timeout = Timeout,
            };

            return client;
        }

        protected virtual void ConfigureHttpClient(HttpClient httpClient)
        {
        }

        protected virtual void ConfigureServicePoint(ServicePoint servicePoint)
        {
            servicePoint.ConnectionLeaseTimeout = (int)ServicePointConnectionLeaseTimeout.TotalMilliseconds;
        }

        protected virtual void ConfigureDefaultRequestHeaders(HttpRequestHeaders headers)
        {
            headers.Clear();
            headers.ConnectionClose = false;
            headers.TryAddWithoutValidation("Accept", "application/json");
            headers.TryAddWithoutValidation("Content-Type", "application/json");

            if (CustomDefaultHttpHeaders != null)
            {
                foreach (var httpHeader in CustomDefaultHttpHeaders)
                {
                    headers.TryAddWithoutValidation(httpHeader.Key, httpHeader.Value);
                }
            }
        }
    }
}