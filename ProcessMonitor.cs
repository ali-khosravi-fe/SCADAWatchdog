using System.Diagnostics;

namespace ScadaWatchdog
{
    public class ProcessMonitor
    {
        private readonly ILogger<ProcessMonitor> _logger;

        private readonly Dictionary<string, Queue<DateTime>> _restartHistory = new();

        private readonly Dictionary<string, DateTime> _gracePeriodUntil = new();

        public ProcessMonitor(ILogger<ProcessMonitor> logger)
        {
            _logger = logger;
        }

        public void Check(ProcessOptions config)
        {
            try
            {
                var now = DateTime.UtcNow;

                // If the process was recently restarted,
                // give it time to initialize.
                if (_gracePeriodUntil.TryGetValue(
                        config.Name,
                        out var gracePeriodUntil) &&
                    now < gracePeriodUntil)
                {
                    _logger.LogDebug(
                        "Process '{ProcessName}' is in grace period. " +
                        "Next check in {Seconds} seconds.",
                        config.Name,
                        (gracePeriodUntil - now).TotalSeconds);

                    return;
                }

                var processes =
                    Process.GetProcessesByName(config.Name);

                if (processes.Length == 0)
                {
                    _logger.LogWarning(
                        "Process '{ProcessName}' is NOT running.",
                        config.Name);

                    if (config.RestartEnabled)
                    {
                        TryRestart(config);
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Process '{ProcessName}' is running. Count: {Count}",
                        config.Name,
                        processes.Length);

                    foreach (var process in processes)
                    {
                        try
                        {
                            if (!process.Responding)
                            {
                                _logger.LogWarning(
                                    "Process '{ProcessName}' is not responding.",
                                    config.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "Could not determine the responding state " +
                                "of process '{ProcessName}'.",
                                config.Name);
                        }

                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while checking process '{ProcessName}'.",
                    config.Name);
            }
        }

        private void TryRestart(ProcessOptions config)
        {
            if (!_restartHistory.ContainsKey(config.Name))
            {
                _restartHistory[config.Name] =
                    new Queue<DateTime>();
            }

            var now = DateTime.UtcNow;

            var history =
                _restartHistory[config.Name];

            // Remove old restart records.
            while (history.Count > 0 &&
                   (now - history.Peek()).TotalSeconds >
                   config.RestartWindowSeconds)
            {
                history.Dequeue();
            }

            // Check restart limit.
            if (history.Count >= config.MaxRestarts)
            {
                _logger.LogError(
                    "Restart limit reached for process '{ProcessName}'. " +
                    "Automatic restart has been blocked.",
                    config.Name);

                return;
            }

            try
            {
                _logger.LogWarning(
                    "Starting process '{ProcessName}'...",
                    config.Name);

                Process.Start(config.Path);

                history.Enqueue(now);

                // Start grace period.
                _gracePeriodUntil[config.Name] =
                    now.AddSeconds(config.GracePeriodSeconds);

                _logger.LogInformation(
                    "Process '{ProcessName}' started. " +
                    "Grace period: {GracePeriodSeconds} seconds. " +
                    "Restart count: {RestartCount}/{MaxRestarts}",
                    config.Name,
                    config.GracePeriodSeconds,
                    history.Count,
                    config.MaxRestarts);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to start process '{ProcessName}'.",
                    config.Name);
            }
        }
    }
}