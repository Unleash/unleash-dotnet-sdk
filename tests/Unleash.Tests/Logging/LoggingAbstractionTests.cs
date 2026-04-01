using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unleash.Logging;
using Unleash.Tests.Mock;

namespace Unleash.Tests.Logging
{
    public class LoggingAbstractionTests
    {
        [TearDown]
        public void TearDown()
        {
            // Reset to default after each test
            LogProvider.SetLoggerFactory(null);
        }

        [Test]
        public void GetLogger_returns_working_ILog_with_no_factory_configured()
        {
            var log = LogProvider.GetLogger(typeof(LoggingAbstractionTests));

            log.Should().NotBeNull();

            // Should not throw — silently discards with NullLoggerFactory
            log.Info(() => "test message");
        }

        [Test]
        public void SetLoggerFactory_causes_logs_to_flow_to_provided_factory()
        {
            var sink = new TestLoggerProvider();
            var factory = LoggerFactory.Create(builder => builder.AddProvider(sink).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            LogProvider.SetLoggerFactory(factory);
            var log = LogProvider.GetLogger(typeof(LoggingAbstractionTests));

            log.Info(() => "hello from test");

            sink.Messages.Should().ContainSingle(m => m.Contains("hello from test"));
        }

        [Test]
        public void Log_levels_map_correctly()
        {
            var sink = new TestLoggerProvider();
            var factory = LoggerFactory.Create(builder => builder.AddProvider(sink).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            LogProvider.SetLoggerFactory(factory);
            var log = LogProvider.GetLogger(typeof(LoggingAbstractionTests));

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

            LogProvider.SetLoggerFactory(factory);
            var log = LogProvider.GetLogger(typeof(LoggingAbstractionTests));

            var ex = new InvalidOperationException("boom");
            log.Error(() => "something failed", ex);

            sink.Exceptions.Should().ContainSingle(e => e == ex);
        }

        [Test]
        public void Lazy_resolution_picks_up_factory_set_after_logger_creation()
        {
            // Logger created before factory is set (simulates static field initializer)
            var log = LogProvider.GetLogger(typeof(LoggingAbstractionTests));

            var sink = new TestLoggerProvider();
            var factory = LoggerFactory.Create(builder => builder.AddProvider(sink).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            // Factory set after logger was created
            LogProvider.SetLoggerFactory(factory);

            log.Info(() => "late-bound message");

            sink.Messages.Should().ContainSingle(m => m.Contains("late-bound message"));
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
