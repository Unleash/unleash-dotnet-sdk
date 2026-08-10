// Logging entry point used by the SDK. On netstandard2.0 it is a thin passthrough
// to LibLog (unchanged behaviour). On net8.0 it dispatches at runtime:
//   1. explicit ILoggerFactory (Microsoft.Extensions.Logging) always wins;
//   2. else if the Unleash.UseLibLog feature switch is on (default), use LibLog;
//   3. else silent.
// On net8.0 the LibLog branch is the SOLE route to LibLog's reflection-based
// provider detection, so when the Unleash.UseLibLog feature switch is trimmed to
// false (see ILLink.Substitutions.xml) that branch is dead and the trimmer removes
// LibLog entirely, making the SDK AOT/trim clean.
using System;
#if NET8_0_OR_GREATER
using Microsoft.Extensions.Logging;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
#endif

namespace Unleash.Logging
{
    internal static class UnleashLog
    {
#if NET8_0_OR_GREATER
        private static ILoggerFactory _loggerFactory;

        internal static void SetLoggerFactory(ILoggerFactory factory) => _loggerFactory = factory;

        private static bool UseLibLog =>
            !AppContext.TryGetSwitch("Unleash.UseLibLog", out var enabled) || enabled;

        internal static ILog GetLogger(Type type, string fallbackTypeName = "System.Object")
            => new DispatchingLog(type?.FullName ?? fallbackTypeName);

        private sealed class DispatchingLog : ILog
        {
            private readonly string _name;
            private ILog _libLog;

            public DispatchingLog(string name) => _name = name;

            public bool Log(LogLevel logLevel, Func<string> messageFunc, Exception exception = null, params object[] formatParameters)
            {
                var factory = _loggerFactory;
                if (factory != null)
                    return MelLog(factory, _name, logLevel, messageFunc, exception, formatParameters);

                if (UseLibLog)
                {
                    _libLog ??= LogProvider.GetLogger(_name);
                    return _libLog.Log(logLevel, messageFunc, exception, formatParameters);
                }

                return false;
            }

            private static bool MelLog(ILoggerFactory factory, string name, LogLevel logLevel, Func<string> messageFunc, Exception exception, object[] formatParameters)
            {
                var logger = factory.CreateLogger(name);
                var melLevel = MapLevel(logLevel);

                if (messageFunc == null)
                    return logger.IsEnabled(melLevel);

                if (!logger.IsEnabled(melLevel))
                    return false;

                var message = messageFunc();
                if (formatParameters != null && formatParameters.Length > 0)
                    message = string.Format(message, formatParameters);

                logger.Log(melLevel, exception, message);
                return true;
            }

            private static MelLogLevel MapLevel(LogLevel level) => level switch
            {
                LogLevel.Trace => MelLogLevel.Trace,
                LogLevel.Debug => MelLogLevel.Debug,
                LogLevel.Info => MelLogLevel.Information,
                LogLevel.Warn => MelLogLevel.Warning,
                LogLevel.Error => MelLogLevel.Error,
                LogLevel.Fatal => MelLogLevel.Critical,
                _ => MelLogLevel.None,
            };
        }
#else
        internal static ILog GetLogger(Type type, string fallbackTypeName = "System.Object")
            => LogProvider.GetLogger(type, fallbackTypeName);
#endif
    }
}
