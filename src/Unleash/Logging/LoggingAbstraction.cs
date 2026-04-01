// This file provides the Unleash.Logging namespace for net8.0+,
// replacing LibLog with Microsoft.Extensions.Logging.
// On netstandard2.0, LibLog.cs is compiled instead.

using System;
using Microsoft.Extensions.Logging;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Unleash.Logging
{
    internal enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }

    internal interface ILog
    {
        bool Log(LogLevel logLevel, Func<string> messageFunc, Exception exception = null, params object[] formatParameters);
    }

    internal static class LogProvider
    {
        private static ILoggerFactory _loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

        internal static ILoggerFactory CurrentFactory => _loggerFactory;

        internal static void SetLoggerFactory(ILoggerFactory factory)
        {
            _loggerFactory = factory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        }

        internal static ILog GetLogger(Type type, string fallbackTypeName = "System.Object")
        {
            var name = type?.FullName ?? fallbackTypeName;
            return new LoggerAdapter(name);
        }

        internal static ILog GetLogger(string name)
        {
            return new LoggerAdapter(name);
        }
    }

    internal class LoggerAdapter : ILog
    {
        private readonly string _categoryName;

        public LoggerAdapter(string categoryName)
        {
            _categoryName = categoryName;
        }

        public bool Log(LogLevel logLevel, Func<string> messageFunc, Exception exception = null, params object[] formatParameters)
        {
            var melLevel = MapLevel(logLevel);
            var logger = LogProvider.CurrentFactory.CreateLogger(_categoryName);

            if (messageFunc == null)
            {
                return logger.IsEnabled(melLevel);
            }

            if (!logger.IsEnabled(melLevel))
                return false;

            var message = messageFunc();
            if (formatParameters != null && formatParameters.Length > 0)
            {
                message = string.Format(message, formatParameters);
            }

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

    internal static class LogExtensions
    {
        public static void Trace(this ILog logger, Func<string> messageFunc, Exception exception = null, params object[] args)
            => Log(logger, LogLevel.Trace, messageFunc, exception, args);

        public static void Debug(this ILog logger, Func<string> messageFunc, Exception exception = null, params object[] args)
            => Log(logger, LogLevel.Debug, messageFunc, exception, args);

        public static void Info(this ILog logger, Func<string> messageFunc, Exception exception = null, params object[] args)
            => Log(logger, LogLevel.Info, messageFunc, exception, args);

        public static void Warn(this ILog logger, Func<string> messageFunc, Exception exception = null, params object[] args)
            => Log(logger, LogLevel.Warn, messageFunc, exception, args);

        public static void Error(this ILog logger, Func<string> messageFunc, Exception exception = null, params object[] args)
            => Log(logger, LogLevel.Error, messageFunc, exception, args);

        public static void Fatal(this ILog logger, Func<string> messageFunc, Exception exception = null, params object[] args)
            => Log(logger, LogLevel.Fatal, messageFunc, exception, args);

        private static void Log(ILog logger, LogLevel logLevel, Func<string> messageFunc, Exception exception, params object[] args)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            if (logger.Log(logLevel, null))
            {
                logger.Log(logLevel, messageFunc, exception, args);
            }
        }
    }
}
