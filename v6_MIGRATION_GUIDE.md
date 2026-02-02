# Migrating to Unleash-Client-Dotnet 6.0.0

This guide highlights the key changes you should be aware of when upgrading to v6.0.0 of the Unleash client.


## Changes to IUnleashScheduledTaskManager APIs

v6 changes how the SDK interacts with the Scheduled task manager. Instead of passing over control of all tasks, 
the SDK has assumed that responsibility and now hands them over for configuration one by one with instructions 
on whether or not to start the scheduling for the task right away. The SDK will during runtime invoke the Start 
and Stop as needed.

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

We've moved the event listener config to the construction of the Unleash instance. 
This is so events triggered as Unleash is starting up are not missed. 
This affects the public constructor of DefaultUnleash as well as the Unleash client factory

### Removed APIs

``` dotnet

void ConfigureEvents(Action<EventCallbackConfig> callback)

```

### Changed APIs

``` dotnet

public DefaultUnleash(UnleashSettings settings, params IStrategy[] strategies)
// becomes
public DefaultUnleash(UnleashSettings settings, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies)
// where the callback is optional, but if custom strategies are in use the parameter needs to be specified like this:
new DefaultUnleash(settings, null, ...) // or new DefaultUnleash(settings, callback: null, ...)

```

## Changes to IUnleashClientFactory APIs

Matching the changes made to DefaultUnleash | IUnleash we've also added the callback as an optional parameter to the 
IUnleashClientFactory methods CreateClient and CreateClientAsync.

### Changed APIs

``` dotnet

IUnleash CreateClient(UnleashSettings settings, bool synchronousInitialization = false, params IStrategy[] strategies);
Task<IUnleash> CreateClientAsync(UnleashSettings settings, bool synchronousInitialization = false, params IStrategy[] strategies);
// becomes
IUnleash CreateClient(UnleashSettings settings, bool synchronousInitialization = false, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies);
Task<IUnleash> CreateClientAsync(UnleashSettings settings, bool synchronousInitialization = false, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies);
// where the callback is optional, but if custom strategies are in use the parameter needs to be specified like this:
CreateClient(settings, false, null, ...) // or CreateClient(settings, callback: null, ...)
await CreateClientAsync(settings, false, null, ...) // or CreateClientAsync(settings, callback: null, ...)

```

## Changes to EventCallbackConfig

The public method RaiseTogglesUpdated has been made internal

## Changes to UnleashSettings

### Removed APIs

We've removed the UnleashSettings Environment property. It's sourced from the API token when available, but can also still be set on the UnleashContext where needed

``` dotnet

public string Environment { get; set; }

```
