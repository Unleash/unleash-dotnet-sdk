using System.Diagnostics;
using System.Security.Cryptography;
using Unleash;
using Unleash.ClientFactory;
using System;
using System.Threading;

class Program
{
    private static readonly ManualResetEvent QuitEvent = new ManualResetEvent(false);

    static async Task Main(string[] args)
    {
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("Ctrl+C pressed. Shutting down...");
            e.Cancel = true; // Prevent immediate process kill
            QuitEvent.Set();
        };

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            Console.WriteLine("Process exiting...");
            QuitEvent.Set();
        };

        var unleashApi = Environment.GetEnvironmentVariable("UNLEASH_API");
        var unleashApiKey = Environment.GetEnvironmentVariable("UNLEASH_API_KEY");
        int.TryParse(Environment.GetEnvironmentVariable("UNLEASH_ENABLED_INTERVAL") ?? "200", out var enabledInterval);
        enabledInterval = enabledInterval > 10 ? enabledInterval : 10;
        var factory = new UnleashClientFactory();
        var unleash = await factory.CreateClientAsync(new UnleashSettings
        {
            AppName = "dotnet-app",
            UnleashApi = new Uri(unleashApi),
            CustomHttpHeaders = new Dictionary<string, string>
            {
                { "Authorization", unleashApiKey }
            },
        });

        unleash.ConfigureEvents((cfg) =>
        {
            cfg.ErrorEvent += (evt) => { Console.WriteLine($"Unleash Error: {evt}"); };
        });

        System.Timers.Timer t = new System.Timers.Timer(200);
        t.Elapsed += (s, e) =>
        {
            var watch = Stopwatch.StartNew();
            var flags = unleash.ListKnownToggles();
            var elementAt = RandomNumberGenerator.GetInt32(flags.Count);
            var variantFlag = flags.ElementAt(elementAt);
            foreach (var flag in flags)
            {
                var enabled = unleash.IsEnabled(flag.Name);
            }
            var variant = unleash.GetVariant(variantFlag.Name);
            watch.Stop();
            var elapsed = watch.ElapsedTicks;
        };
        t.Start();

        QuitEvent.WaitOne();
    }
}
