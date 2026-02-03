# Migrating to Unleash-Client-Dotnet 6.0.0

This guide describes breaking changes in version 6.0.0 of the Unleash .NET SDK. 
Follow this guide if you're upgrading from version 5.x and use any of the following features:
- Custom scheduled task managers.
- Event listeners.
- The `UnleashClientFactory` class.
- The `Environment` property on `UnleashSettings.`


## Changes to IUnleashScheduledTaskManager APIs

v6 changes how the SDK interacts with the Scheduled task manager. 
If you have not implemented a custom scheduler this will not affect you.

If you have implemented a custom scheduler that you've registered on the UnleashSettings when instantiating Unleash,
take a look at the changed APIs below. Unleash .NET SDK now owns the responsibility for the tasks and interacts with 
the scheduler when it needs the tasks configured, started, or stopped.

### Removed APIs

``` dotnet

void Configure(IEnumerable<IUnleashScheduledTask> tasks, CancellationToken cancellationToken);

```

### Added APIs

``` dotnet

void ConfigureTask(IUnleashScheduledTask task, CancellationToken cancellationToken, bool start);
void Start(IUnleashScheduledTask task);
void Stop(IUnleashScheduledTask task);

```

## DefaultUnleash | IUnleash

The API to configure event listeners for emitted events (Impression events, ready, error etc) have been moved to 
the constructor of DefaultUnleash to not miss events fired during initialization. If you were using this feature take a 
look at the changed APIs below for how to update your implementation

### Changed APIs

``` dotnet

public DefaultUnleash(UnleashSettings settings, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies)

// where the callback is a new parameter and optional, but if custom strategies are in use the parameter needs to be specified like this:
new DefaultUnleash(settings, null, ...) // or new DefaultUnleash(settings, callback: null, ...)

```

### Removed APIs

``` dotnet

void ConfigureEvents(Action<EventCallbackConfig> callback)

```

## Changes to IUnleashClientFactory APIs

Matching the changes made to DefaultUnleash | IUnleash we've also added the callback as an optional parameter to the 
IUnleashClientFactory methods CreateClient and CreateClientAsync.

### Changed APIs

``` dotnet

IUnleash CreateClient(UnleashSettings settings, bool synchronousInitialization = false, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies);
Task<IUnleash> CreateClientAsync(UnleashSettings settings, bool synchronousInitialization = false, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies);

// where the callback is optional, but if custom strategies are in use the parameter needs to be specified like this:
CreateClient(settings, false, null, ...) // or CreateClient(settings, callback: null, ...)
await CreateClientAsync(settings, false, null, ...) // or CreateClientAsync(settings, callback: null, ...)

```

## Changes to EventCallbackConfig

The public methods RaiseTogglesUpdated and RaiseError have been made internal

## Changes to UnleashSettings

### Removed APIs

We've removed the UnleashSettings Environment property. It's sourced from the API token when available, but can also still be set on the UnleashContext where needed

``` dotnet

public string Environment { get; set; }

```
