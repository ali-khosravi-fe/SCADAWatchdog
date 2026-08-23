namespace ScadaWatchdog
{
    public class WatchdogOptions
    {
        public int CheckIntervalSeconds { get; set; } = 5;

        public List<ProcessOptions> Processes { get; set; } = new();
    }

    public class ProcessOptions
    {
        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public bool RestartEnabled { get; set; } = true;

        public int MaxRestarts { get; set; } = 3;

        public int RestartWindowSeconds { get; set; } = 60;

        public int GracePeriodSeconds { get; set; } = 10;
    }
}