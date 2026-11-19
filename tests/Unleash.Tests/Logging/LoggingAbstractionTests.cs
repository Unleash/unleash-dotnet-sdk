using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Unleash.Logging;
using Unleash.Tests.Mock;

namespace Unleash.Tests.Logging
{
    public class LoggingAbstractionTests
    {
        [TearDown]
        public void TearDown()
        {
            // Reset shared state after each test.
            UnleashLog.SetLoggerFactory(null);
            // Restore the feature switch to its default (on).
            AppContext.SetSwitch("Unleash.UseLibLog", true);
        }

        [Test]
        public void GetLogger_returns_working_ILog_with_no_factory_configured()
        {
            var log = UnleashLog.GetLogger(typeof(LoggingAbstractionTests));

            log.Should().NotBeNull();

            // No factory + default switch (on) routes to LibLog; with no logging
            // provider present it resolves to a no-op logger and must not throw.
            log.Info(() => "test message");
        }

        [Test]
        public void SetLoggerFactory_causes_logs_to_flow_to_provided_factory()
        {
            var sink = new TestLoggerProvider();
            var factory = LoggerFactory.Create(builder => builder.AddProvider(sink).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            UnleashLog.SetLoggerFactory(factory);
            var log = UnleashLog.GetLogger(typeof(LoggingAbstractionTests));

            log.Info(() => "hello from test");

            sink.Messages.Should().ContainSingle(m => m.Contains("hello from test"));
        }

        [Test]
        public void Log_levels_map_correctly()
        {
            var sink = new TestLoggerProvider();
            var factory = LoggerFactory.Create(builder => builder.AddProvider(sink).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            UnleashLog.SetLoggerFactory(factory);
            var log = UnleashLog.GetLogger(typeof(LoggingAbstractionTests));

            log.Trace(() => "trace msg");
            log.Debug(() => "debug msg");
            log.Info(() => "info msg");
            log.Warn(() => "warn msg");
            log.Error(() => "error msg");

            sink.Messages.Should().HaveCount(5);
            sink.Levels.Should().ContainInOrder(
                Microsoft.Extensions.Logging.LogLevel.Trace,
                Microsoft.Extensions.Logging.LogLevel.Debug,
                Microsoft.Extensions.Logging.LogLevel.Information,
                Microsoft.Extensions.Logging.LogLevel.Warning,
                Microsoft.Extensions.Logging.LogLevel.Error
            );
        }

        [Test]
        public void Log_includes_exception_when_provided()
        {
            var sink = new TestLoggerProvider();
            var factory = LoggerFactory.Create(builder => builder.AddProvider(sink).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            UnleashLog.SetLoggerFactory(factory);
            var log = UnleashLog.GetLogger(typeof(LoggingAbstractionTests));

            var ex = new InvalidOperationException("boom");
            log.Error(() => "something failed", ex);

            sink.Exceptions.Should().ContainSingle(e => e == ex);
        }

        [Test]
        public void Lazy_resolution_picks_up_factory_set_after_logger_creation()
        {
            // Logger created before factory is set (simulates static field initializer)
            var log = UnleashLog.GetLogger(typeof(LoggingAbstractionTests));

            var sink = new TestLoggerProvider();
            var factory = LoggerFactory.Create(builder => builder.AddProvider(sink).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            // Factory set after logger was created
            UnleashLog.SetLoggerFactory(factory);

            log.Info(() => "late-bound message");

            sink.Messages.Should().ContainSingle(m => m.Contains("late-bound message"));
        }

        [Test]
        public void Explicit_factory_wins_even_when_UseLibLog_switch_is_on()
        {
            AppContext.SetSwitch("Unleash.UseLibLog", true);

            var sink = new TestLoggerProvider();
            var factory = LoggerFactory.Create(builder => builder.AddProvider(sink).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            UnleashLog.SetLoggerFactory(factory);
            var log = UnleashLog.GetLogger(typeof(LoggingAbstractionTests));

            log.Info(() => "factory-wins message");

            sink.Messages.Should().ContainSingle(m => m.Contains("factory-wins message"));
        }

        [Test]
        public void UseLibLog_switch_off_with_no_factory_is_silent()
        {
            AppContext.SetSwitch("Unleash.UseLibLog", false);

            var log = UnleashLog.GetLogger(typeof(LoggingAbstractionTests));

            // No factory + switch off => silent. Log returns false and does not throw.
            log.Log(Unleash.Logging.LogLevel.Error, () => "dropped").Should().BeFalse();
        }

        [Test]
        public void DefaultUnleash_wires_LoggerFactory_from_settings()
        {
            var sink = new TestLoggerProvider();
            var factory = LoggerFactory.Create(builder => builder.AddProvider(sink).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            var settings = new MockedUnleashSettings
            {
                LoggerFactory = factory
            };

            using var unleash = new DefaultUnleash(settings);

            // The SDK logs an Info message during construction
            sink.Messages.Should().Contain(m => m.Contains("UNLEASH:"));
        }

        [Test]
        public void AddUnleash_resolves_LoggerFactory_from_DI()
        {
            var sink = new TestLoggerProvider();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(sink).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));
            services.AddSingleton<MockApiClient>();
            services.AddUnleash(settings =>
            {
                settings.AppName = "test-di";
                settings.UnleashApi = new Uri("http://localhost:4242/");
                settings.DisableSingletonWarning = true;
                settings.UnleashApiClient = new MockApiClient();
                settings.FileSystem = new MockFileSystem();
            });

            var provider = services.BuildServiceProvider();
            using var unleash = provider.GetRequiredService<IUnleash>();

            unleash.Should().NotBeNull();
            sink.Messages.Should().Contain(m => m.Contains("UNLEASH:"));
        }

        private class TestLoggerProvider : ILoggerProvider
        {
            public List<string> Messages { get; } = new();
            public List<Microsoft.Extensions.Logging.LogLevel> Levels { get; } = new();
            public List<Exception> Exceptions { get; } = new();

            public ILogger CreateLogger(string categoryName) => new TestLogger(this);

            public void Dispose() { }

            private class TestLogger : ILogger
            {
                private readonly TestLoggerProvider _provider;

                public TestLogger(TestLoggerProvider provider) => _provider = provider;

                public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

                public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    _provider.Messages.Add(formatter(state, exception));
                    _provider.Levels.Add(logLevel);
                    if (exception != null)
                        _provider.Exceptions.Add(exception);
                }
            }
        }
    }
}
