# Migrating to Unleash-Client-Dotnet 6.0.0

This guide describes breaking changes in version 6.0.0 of the Unleash .NET SDK. 
Follow this guide if you're upgrading from version 5.x and use any of the following features:
- Custom scheduled task managers.
- Event listeners.
- The `UnleashClientFactory` class.
- The `Environment` property on `UnleashSettings.`


## Changes to IUnleashScheduledTaskManager APIs

If you registered a custom scheduler on `UnleashSettings` when instantiating Unleash, review the changed APIs below. The Unleash .NET SDK now owns responsibility for the tasks and interacts with the scheduler when it needs the tasks configured, started, or stopped.

### Removed APIs

``` csharp

void Configure(IEnumerable<IUnleashScheduledTask> tasks, CancellationToken cancellationToken);

```

### Added APIs

``` csharp

void ConfigureTask(IUnleashScheduledTask task, CancellationToken cancellationToken, bool start);
void Start(IUnleashScheduledTask task);
void Stop(IUnleashScheduledTask task);

```

## DefaultUnleash | IUnleash

The event listener configuration API has moved to the `DefaultUnleash` constructor to prevent missing [events](https://docs.getunleash.io/concepts/impression-data) fired during initialization. If you use this feature, review the changed APIs below to update your implementation.

### Changed APIs

``` csharp

public DefaultUnleash(UnleashSettings settings, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies)

````

The `callback` parameter is new and optional. If you use custom strategies, specify the callback parameter explicitly:

```csharp

new DefaultUnleash(settings, null, ...) // or new DefaultUnleash(settings, callback: null, ...)

```

### Removed APIs

``` csharp

void ConfigureEvents(Action<EventCallbackConfig> callback)

```

## Changes to IUnleashClientFactory APIs

To match the changes made to `DefaultUnleash`, the `callback` parameter has been added as an optional parameter to the `IUnleashClientFactory` methods `CreateClient` and `CreateClientAsync`.

### Changed APIs

``` csharp

IUnleash CreateClient(UnleashSettings settings, bool synchronousInitialization = false, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies);
Task<IUnleash> CreateClientAsync(UnleashSettings settings, bool synchronousInitialization = false, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies);

// The `callback` parameter is optional. If you use custom strategies, specify the callback parameter explicitly:
CreateClient(settings, false, null, ...) // or CreateClient(settings, callback: null, ...)
await CreateClientAsync(settings, false, null, ...) // or CreateClientAsync(settings, callback: null, ...)

```

## Changes to EventCallbackConfig

The `RaiseTogglesUpdated` and `RaiseError` methods are now internal.The `Environment` property has been removed from `UnleashSettings`. The SDK sources this value from the API token when available. If you need to set the environment explicitly, set it on `UnleashContext` instead.

``` csharp

public string Environment { get; set; }

```
