using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

public class ReminderWorker : BackgroundService
{
    private readonly IConfiguration _configuration;

    public ReminderWorker(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            string connString = _configuration.GetConnectionString("TravelAgencyDB");

            await SharedLogic.CheckAndSend5DayReminders(connString);

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}