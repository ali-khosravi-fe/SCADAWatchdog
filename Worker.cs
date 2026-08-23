using Microsoft.Extensions.Options;

namespace ScadaWatchdog
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly WatchdogOptions _options;
        private readonly ProcessMonitor _processMonitor;

        public Worker(
            ILogger<Worker> logger,
            IOptions<WatchdogOptions> options,
            ProcessMonitor processMonitor)
        {
            _logger = logger;
            _options = options.Value;
            _processMonitor = processMonitor;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "SCADA Watchdog started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var processConfig in _options.Processes)
                {
                    _processMonitor.Check(processConfig);
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(
                        _options.CheckIntervalSeconds),
                    stoppingToken);
            }
        }
    }
}