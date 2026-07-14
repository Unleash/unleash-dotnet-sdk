using System;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unleash.Logging;

namespace Unleash.Internal
{
    internal static class UnleashExtensions
    {
        private static readonly ILog Logger = UnleashLog.GetLogger(typeof(UnleashExtensions));

        internal static void AddContentTypeJson(this HttpContentHeaders headers)
        {
            const string contentType = "Content-Type";
            const string applicationJson = "application/json";

            headers.TryAddWithoutValidation(contentType, applicationJson);
        }

        internal static string ConvertToString(this Stream stream)
        {
            stream.Position = 0;

            using (stream)
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        internal static void SafeTimerChange(this Timer timer, int dueTime, int period, ref bool disposeEnded)
        {
            try
            {
                timer?.Change(dueTime, period);
            }
            catch (ObjectDisposedException)
            {
                // race condition with Dispose can cause trigger to be called when underlying
                // timer is being disposed - and a change will fail in this case.
                // see
                // https://msdn.microsoft.com/en-us/library/b97tkt95(v=vs.110).aspx#Anchor_2
                if (disposeEnded)
                {
                    // we still want to throw the exception in case someone really tries
                    // to change the timer after disposal has finished
                    // of course there's a slight race condition here where we might not
                    // throw even though disposal is already done.
                    // since the offending code would most likely already be "failing"
                    // unreliably i personally can live with increasing the
                    // "unreliable failure" time-window slightly
                    throw;
                }
            }
        }

        internal static void SafeTimerChange(this Timer timer, TimeSpan dueTime, TimeSpan period, ref bool disposeEnded)
        {
            try
            {
                timer?.Change(dueTime, period);
            }
            catch (ObjectDisposedException)
            {
                // race condition with Dispose can cause trigger to be called when underlying
                // timer is being disposed - and a change will fail in this case.
                // see
                // https://msdn.microsoft.com/en-us/library/b97tkt95(v=vs.110).aspx#Anchor_2
                if (disposeEnded)
                {
                    // we still want to throw the exception in case someone really tries
                    // to change the timer after disposal has finished
                    // of course there's a slight race condition here where we might not
                    // throw even though disposal is already done.
                    // since the offending code would most likely already be "failing"
                    // unreliably i personally can live with increasing the
                    // "unreliable failure" time-window slightly
                    throw;
                }
            }
        }

        internal static Stream ConvertToStream(this string s)
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024 * 4, true))
            {
                writer.Write(s);
                writer.Flush();
            }
            stream.Position = 0;
            return stream;
        }

        internal static string GetLocalIpAddress()
        {
            try
            {
                var hostname = Environment.GetEnvironmentVariable("hostname");
                if (hostname != null)
                    return hostname;

                var host = Dns.GetHostEntry(Dns.GetHostName());

                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }

                return "undefined-ip-address";
            }
            catch (Exception exception)
            {
                Logger.Trace(() => "UNLEASH: Failed to extract local ip address", exception);
                return "undefined";
            }
        }
    }
}