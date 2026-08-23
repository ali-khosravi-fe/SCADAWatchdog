using Microsoft.Extensions.Hosting;

namespace ScadaWatchdog
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Run as a Windows Service
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "Scada Watchdog";
            });

            // Load Watchdog settings from appsettings.json
            builder.Services.Configure<WatchdogOptions>(
                builder.Configuration.GetSection("Watchdog"));

            // Register the Worker
            builder.Services.AddSingleton<ProcessMonitor>();
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();

            host.Run();
        }
    }
}