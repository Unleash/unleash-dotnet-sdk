namespace Unleash
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A context which the feature request should be validated against. Usually scoped to a web request through an implementation of IUnleashContextProvider.
    /// </summary>
    public class UnleashContext : Yggdrasil.Context
    {
        public UnleashContext()
        {
            Properties = new Dictionary<string, string>();
        }

        public UnleashContext(string appName, string environment, string userId, string sessionId, string remoteAddress, DateTimeOffset? currentTime, Dictionary<string, string> properties)
        {
            AppName = appName;
            Environment = environment;
            UserId = userId;
            SessionId = sessionId;
            RemoteAddress = remoteAddress;
            CurrentTime = currentTime;
            Properties = properties;
        }

        public string GetByName(string contextName)
        {
            switch (contextName)
            {
                case "environment":
                    return Environment;
                case "appName":
                    return AppName;
                case "userId":
                    return UserId;
                case "sessionId":
                    return SessionId;
                case "remoteAddress":
                    return RemoteAddress;
                case "currentTime":
                    return (CurrentTime ?? DateTimeOffset.UtcNow).ToString("O");
                default:
                    string result;
                    Properties.TryGetValue(contextName, out result);
                    return result;
            }
        }

        public UnleashContext ApplyStaticFields(UnleashSettings settings)
        {
            Environment = string.IsNullOrEmpty(Environment) ? GetTokenEnvironment(settings) : Environment;
            AppName = string.IsNullOrEmpty(AppName) ? settings.AppName : AppName;
            return this;
        }

        internal string GetTokenEnvironment(UnleashSettings settings)
        {
            settings.CustomHttpHeaders.TryGetValue("Authorization", out var token);
            if (string.IsNullOrEmpty(token) || !token.Contains(":") || !token.Contains("."))
            {
                return "default";
            }

            return token.Split(':', '.')[1];
        }

        internal static Builder New()
        {
            return new Builder();
        }

        internal class Builder
        {
            private string appName;
            private string environment;
            private string userId;
            private string sessionId;
            private string remoteAddress;
            private DateTimeOffset? currentTime;
            private readonly Dictionary<string, string> properties;

            public Builder()
            {
                properties = new Dictionary<string, string>();
            }

            public Builder(UnleashContext context)
            {
                appName = context.AppName;
                environment = context.Environment;
                userId = context.UserId;
                sessionId = context.SessionId;
                remoteAddress = context.RemoteAddress;
                currentTime = context.CurrentTime;
                properties = new Dictionary<string, string>(context.Properties);
            }

            public Builder AppName(string appName)
            {
                this.appName = appName;
                return this;
            }

            public Builder Environment(string environment)
            {
                this.environment = environment;
                return this;
            }

            public Builder UserId(string userId)
            {
                this.userId = userId;
                return this;
            }

            public Builder SessionId(string sessionId)
            {
                this.sessionId = sessionId;
                return this;
            }

            public Builder RemoteAddress(string remoteAddress)
            {
                this.remoteAddress = remoteAddress;
                return this;
            }

            public Builder CurrentTime(DateTimeOffset currentTime)
            {
                this.currentTime = currentTime;
                return this;
            }

            public Builder Now()
            {
                this.currentTime = DateTimeOffset.UtcNow;
                return this;
            }

            public Builder AddProperty(string name, string value)
            {
                properties.Add(name, value);
                return this;
            }

            public UnleashContext Build()
                => new UnleashContext(appName, environment, userId, sessionId, remoteAddress, currentTime, properties);
        }
    }
}