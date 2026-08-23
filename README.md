# SCADA Watchdog

A lightweight and configurable Windows Service for monitoring and automatically restarting critical SCADA-related processes.

SCADA systems often rely on multiple background processes that must remain continuously available. If one of these processes crashes or becomes unresponsive, manual intervention can introduce unnecessary downtime.

**SCADA Watchdog** is designed to continuously monitor configured Windows processes and automatically restart them when necessary.

The project is built with **C# and .NET 10 Worker Service** and is intended to run as a **Windows Service**.

---

## Features

* Monitor multiple Windows processes
* Automatically restart stopped processes
* Configurable process executable paths
* Configurable monitoring interval
* Per-process restart configuration
* Maximum restart limit
* Restart time window
* Grace period after process restart
* Basic process responsiveness detection
* Structured logging using `ILogger`
* Windows Service support
* Configuration through `appsettings.json`
* Separation between scheduling and process-monitoring logic
* Designed to be extensible for industrial/SCADA environments

---

## Architecture

The current architecture is intentionally simple and modular:

```text
┌──────────────────────────┐
│      Windows Service     │
│      Scada Watchdog      │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│          Worker          │
│                          │
│ Scheduling / Coordination│
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│     ProcessMonitor       │
│                          │
│ Process Detection        │
│ Restart                  │
│ Restart Limiting         │
│ Grace Period             │
│ Responsiveness Check     │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│     Windows Processes    │
│                          │
│ SCADA Process 1          │
│ SCADA Process 2          │
│ SCADA Process 3          │
└──────────────────────────┘
```

### Main Components

#### `Program.cs`

Responsible for configuring the application host and registering services.

It also configures the application to run as a Windows Service:

```csharp
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Scada Watchdog";
});
```

#### `Worker.cs`

The `Worker` is responsible for the monitoring cycle.

It reads the configured monitoring interval and asks `ProcessMonitor` to check each configured process.

The Worker intentionally contains minimal process-management logic.

#### `ProcessMonitor.cs`

Responsible for monitoring individual processes.

Current responsibilities include:

* Detecting whether a process is running
* Detecting basic responsiveness
* Starting a process when it is not running
* Tracking restart history
* Enforcing restart limits
* Applying a grace period after restart
* Logging monitoring and restart events

#### `WatchdogOptions.cs`

Contains the configuration models used by the application.

---

## Configuration

All monitored processes are configured through:

```text
appsettings.json
```

Example:

```json
{
  "Watchdog": {
    "CheckIntervalSeconds": 5,

    "Processes": [
      {
        "Name": "notepad",
        "Path": "C:\\Windows\\System32\\notepad.exe",
        "RestartEnabled": true,
        "MaxRestarts": 3,
        "RestartWindowSeconds": 60,
        "GracePeriodSeconds": 10
      }
    ]
  },

  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### Configuration Properties

| Property               | Description                                                   |                Example |
| ---------------------- | ------------------------------------------------------------- | ---------------------: |
| `CheckIntervalSeconds` | Global interval between monitoring cycles                     |                    `5` |
| `Name`                 | Process name without `.exe`                                   |              `notepad` |
| `Path`                 | Full path to the executable                                   | `C:\SCADA\App\App.exe` |
| `RestartEnabled`       | Enables automatic restart                                     |                 `true` |
| `MaxRestarts`          | Maximum number of automatic restarts in the configured window |                    `3` |
| `RestartWindowSeconds` | Time window used for restart limiting                         |                   `60` |
| `GracePeriodSeconds`   | Time allowed for a restarted process to initialize            |                   `10` |

---

## Example: Monitoring Multiple Processes

The Watchdog can monitor multiple processes simultaneously.

```json
{
  "Watchdog": {
    "CheckIntervalSeconds": 5,

    "Processes": [
      {
        "Name": "ScadaServer",
        "Path": "C:\\SCADA\\Server\\ScadaServer.exe",
        "RestartEnabled": true,
        "MaxRestarts": 3,
        "RestartWindowSeconds": 60,
        "GracePeriodSeconds": 15
      },
      {
        "Name": "ScadaDriver",
        "Path": "C:\\SCADA\\Driver\\ScadaDriver.exe",
        "RestartEnabled": true,
        "MaxRestarts": 5,
        "RestartWindowSeconds": 120,
        "GracePeriodSeconds": 20
      },
      {
        "Name": "ScadaLogger",
        "Path": "C:\\SCADA\\Logger\\ScadaLogger.exe",
        "RestartEnabled": true,
        "MaxRestarts": 3,
        "RestartWindowSeconds": 60,
        "GracePeriodSeconds": 10
      }
    ]
  }
}
```

This allows each process to have its own restart policy.

---

## Restart Protection

One of the most important design considerations in a SCADA watchdog is avoiding an infinite restart loop.

For example, if a process starts and immediately crashes, a naive watchdog might do this forever:

```text
Process crashes
      ↓
Watchdog detects failure
      ↓
Process starts
      ↓
Process crashes
      ↓
Watchdog detects failure
      ↓
Process starts
      ↓
...
```

SCADA Watchdog uses a restart history to limit automatic restarts.

For example:

```text
MaxRestarts = 3
RestartWindowSeconds = 60
```

means that the Watchdog will allow up to three automatic restarts within a 60-second window.

If the limit is reached, automatic restarting is temporarily blocked and an error is logged.

This provides protection against unstable applications and restart loops.

---

## Grace Period

After restarting a process, the Watchdog does not immediately attempt another restart.

For example:

```text
GracePeriodSeconds = 10
```

results in:

```text
Process failure
      ↓
Restart process
      ↓
Wait 10 seconds
      ↓
Check process again
```

This is useful for SCADA applications that require several seconds to initialize.

Without a grace period, a slow-starting process could potentially be interpreted as failed multiple times.

---

## Process Responsiveness

The Watchdog currently performs a basic responsiveness check using:

```csharp
process.Responding
```

This can detect certain cases where a GUI-based Windows application is running but is no longer responding.

However, `Process.Responding` should **not** be considered a complete industrial health check.

A process can technically respond to Windows messages while its internal SCADA functionality is unhealthy.

For production environments, a stronger health-check mechanism should be considered.

Possible future approaches include:

* Application heartbeat
* TCP health endpoint
* Named pipe heartbeat
* IPC health check
* Shared-memory heartbeat
* SCADA-specific status check
* Application-generated heartbeat file
* Internal diagnostic API

---

## Installation as a Windows Service

After publishing the application, the output directory can be used to install the application as a Windows Service.

For example:

```text
C:\ScadaWatchdog
```

The published executable should be:

```text
C:\ScadaWatchdog\ScadaWatchdog.exe
```

Open **Command Prompt as Administrator** and create the service:

```bat
sc.exe create ScadaWatchdog ^
    binPath= "C:\ScadaWatchdog\ScadaWatchdog.exe" ^
    start= auto ^
    DisplayName= "SCADA Watchdog"
```

Start the service:

```bat
sc.exe start ScadaWatchdog
```

Check the service:

```bat
sc.exe query ScadaWatchdog
```

Stop the service:

```bat
sc.exe stop ScadaWatchdog
```

Remove the service:

```bat
sc.exe delete ScadaWatchdog
```

> The service installation commands assume that the application has already been published and that `ScadaWatchdog.exe` exists in the specified directory.

---

## Windows Service Recovery

A major design goal of this project is that the Watchdog itself should also be protected.

There is an important distinction:

```text
SCADA Process
      ↑
      │
SCADA Watchdog
      ↑
      │
Windows Service Manager
```

The Watchdog monitors SCADA processes.

Windows Service Control Manager monitors the Watchdog service itself.

This avoids creating another custom "watchdog for the watchdog" application.

Windows can be configured to restart the Watchdog if the service process terminates unexpectedly.

Example:

```bat
sc.exe failure ScadaWatchdog ^
    actions= restart/60000/restart/60000/restart/60000 ^
    reset= 86400
```

This configuration requests automatic recovery after service failures.

The exact recovery policy should be selected according to the operational requirements of the target SCADA system.

---

## Publishing

The project is designed to be published for Windows x64.

Recommended publish settings:

```text
Configuration:
Release

Target Runtime:
win-x64

Deployment Mode:
Self-contained
```

Example output directory:

```text
C:\ScadaWatchdog
```

A self-contained deployment avoids depending on a separately installed .NET runtime on the target machine.

---

## Development

### Requirements

* Visual Studio with .NET 10 support
* .NET 10 SDK
* Windows
* Administrator privileges for Windows Service installation

### Build

From Visual Studio:

```text
Build → Build Solution
```

Or using the .NET CLI:

```bat
dotnet build
```

### Run During Development

The application can initially be run directly from Visual Studio.

This is useful for testing the monitoring logic before installing it as a Windows Service.

For example:

```text
F5
```

or:

```bat
dotnet run
```

---

## Project Structure

```text
ScadaWatchdog/
│
├── Program.cs
│
├── Worker.cs
│
├── ProcessMonitor.cs
│
├── WatchdogOptions.cs
│
├── appsettings.json
│
└── ScadaWatchdog.csproj
```

### Responsibility Overview

```text
Program.cs
    │
    ├── Host configuration
    ├── Windows Service configuration
    └── Dependency Injection
            │
            ▼
Worker.cs
    │
    ├── Monitoring schedule
    └── Process iteration
            │
            ▼
ProcessMonitor.cs
    │
    ├── Process detection
    ├── Responsiveness detection
    ├── Restart
    ├── Restart history
    └── Grace period
            │
            ▼
WatchdogOptions.cs
    │
    └── Configuration models
            │
            ▼
appsettings.json
```

---

## Logging

The application uses the standard .NET logging infrastructure:

```csharp
ILogger<T>
```

Examples of events include:

```text
SCADA Watchdog started.

Process 'ScadaServer' is running.

Process 'ScadaDriver' is NOT running.

Starting process 'ScadaDriver'...

Process 'ScadaDriver' started.

Restart limit reached for process 'ScadaDriver'.

Process 'ScadaServer' is not responding.
```

For a production deployment, logging can later be extended with:

* Windows Event Log
* Rolling log files
* Structured JSON logs
* Centralized logging
* SIEM integration
* Alarm/notification integration

---

## Design Goals

The project is intended to follow several principles important for SCADA environments.

### Reliability

The Watchdog should continue monitoring processes with minimal intervention.

### Predictability

Automatic restart behavior should be controlled through explicit configuration.

### Fault Isolation

A failure of one monitored process should not prevent monitoring of other processes.

### Observability

Important events should be logged so that operators and engineers can understand what happened.

### Extensibility

The architecture should allow additional health checks, notification mechanisms and recovery strategies to be added later.

---

## Current Limitations

This project is currently a foundation for a more complete SCADA process-supervision system.

The following areas can be improved for production use:

* More robust health checks
* Process startup timeout detection
* Process dependency management
* Per-process monitoring intervals
* Windows Event Log integration
* Persistent restart history
* Alarm/notification system
* Configuration validation
* Configuration reload without restarting the service
* Graceful shutdown handling
* Process exit event monitoring
* CPU and memory monitoring
* Process startup failure diagnostics
* Multi-instance process policies
* High Availability / redundant watchdog architecture
* Centralized monitoring
* Security hardening
* Service account configuration

---

## Roadmap

### Phase 1 — Core Watchdog

* [x] Worker Service
* [x] Windows Service support
* [x] Process monitoring
* [x] Automatic process restart
* [x] Multiple process configuration
* [x] Restart limits
* [x] Restart time window
* [x] Grace period
* [x] Basic responsiveness detection

### Phase 2 — Production Hardening

* [ ] Windows Event Log
* [ ] Persistent logging
* [ ] Configuration validation
* [ ] Process startup timeout
* [ ] Better exception handling
* [ ] Process exit event handling
* [ ] Per-process check intervals
* [ ] Improved restart state machine

### Phase 3 — Advanced Health Monitoring

* [ ] Application heartbeat
* [ ] TCP/IPC health checks
* [ ] Custom health-check providers
* [ ] CPU monitoring
* [ ] Memory monitoring
* [ ] Process dependency checks

### Phase 4 — SCADA Operations

* [ ] Alarm notifications
* [ ] Email/SMS integration
* [ ] Central monitoring
* [ ] Operator dashboard
* [ ] Audit trail
* [ ] Redundant Watchdog instances
* [ ] Failover architecture

---

## Important Production Considerations

This project is intended to be a **process supervision component**, not a replacement for a complete SCADA High Availability architecture.

For critical industrial systems, process monitoring should be considered as one layer of the overall reliability strategy.

A robust architecture may look like:

```text
                    ┌───────────────────────┐
                    │   SCADA Application   │
                    └───────────┬───────────┘
                                │
                    ┌───────────▼───────────┐
                    │    Process Watchdog   │
                    └───────────┬───────────┘
                                │
                    ┌───────────▼───────────┐
                    │ Windows Service        │
                    │ Recovery Mechanism     │
                    └───────────┬───────────┘
                                │
                    ┌───────────▼───────────┐
                    │ OS / Server Monitoring │
                    └───────────────────────┘
```

For systems with strict availability requirements, this should be complemented by server redundancy, SCADA redundancy, network redundancy and appropriate operational procedures.

---

## Security Considerations

The Watchdog launches configured executables, so configuration files should be protected from unauthorized modification.

Recommended practices include:

* Restrict write access to the installation directory
* Run the service using an appropriate service account
* Avoid running with unnecessary administrative privileges
* Protect executable paths and configuration files
* Validate configured executable paths
* Monitor changes to configuration
* Keep the operating system and .NET runtime patched
* Use application allow-listing where appropriate

---

## Example Use Case

Imagine a SCADA system with three critical processes:

```text
ScadaServer.exe
ScadaDriver.exe
ScadaLogger.exe
```

The desired behavior is:

```text
                    SCADA Watchdog
                          │
             ┌────────────┼────────────┐
             │            │            │
             ▼            ▼            ▼
        ScadaServer  ScadaDriver  ScadaLogger
             │            │            │
             │            X            │
             │         CRASHED         │
             │            │            │
             │            ▼            │
             │         RESTART         │
             │            │            │
             │       Grace Period      │
             │            │            │
             └────────────┼────────────┘
                          │
                    Continue Monitoring
```

The goal is to recover individual process failures without unnecessarily restarting the entire SCADA system.

---

## Contributing

Contributions, ideas and improvements are welcome.

Before submitting a change, consider:

1. Does the change improve reliability?
2. Does it preserve predictable restart behavior?
3. Does it introduce any unsafe behavior for production SCADA systems?
4. Is the behavior properly logged?
5. Is the configuration documented?

---

## License

This project is currently distributed without a specified open-source license.

If this repository is intended for public open-source use, add an appropriate license such as MIT, Apache-2.0, or another license that matches the project's intended usage.

---

## Author

**SCADA Watchdog**

A lightweight process-supervision framework built with C# and .NET for SCADA and industrial Windows environments.

---

## Disclaimer

This software is intended as a process-monitoring and recovery component.

Industrial control systems can have safety, operational and regulatory requirements that cannot be addressed by a process watchdog alone.

Always test recovery behavior in a controlled environment before deploying changes to a production SCADA system.
