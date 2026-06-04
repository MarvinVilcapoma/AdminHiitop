namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IEmailService
{
    bool IsConfigured { get; }
    Task SendAsync(string to, string subject, string htmlBody);
    Task<(int SentToday, int Limit)> GetDailyStatusAsync();
}
