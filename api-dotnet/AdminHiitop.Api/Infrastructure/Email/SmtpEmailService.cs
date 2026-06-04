using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Shared.Helpers;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AdminHiitop.Api.Infrastructure.Email;

public sealed class SmtpOptions
{
    public bool   Enabled    { get; set; } = false;
    public string Host       { get; set; } = "smtp-relay.brevo.com";
    public int    Port       { get; set; } = 587;
    public string Username   { get; set; } = string.Empty;
    public string Password   { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName   { get; set; } = "Hiitop";
    /// <summary>Max emails per calendar day (Peru time). Default: 250 to stay under Brevo's free 300/day limit.</summary>
    public int    DailyLimit { get; set; } = 250;
}

/// <summary>Thread-safe daily email counter. Resets at midnight Peru time.</summary>
public sealed class DailyEmailCounter
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateOnly _currentDay = DateOnly.MinValue;
    private int _sentToday;

    /// <summary>
    /// Returns (allowed, sentToday, limit).
    /// If allowed, increments the counter atomically.
    /// </summary>
    public async Task<(bool Allowed, int SentToday, int Limit)> TryConsumeAsync(int limit)
    {
        await _lock.WaitAsync();
        try
        {
            DateOnly today = DateOnly.FromDateTime(PeruClock.Now);
            if (today != _currentDay)
            {
                _currentDay = today;
                _sentToday  = 0;
            }

            if (_sentToday >= limit)
                return (false, _sentToday, limit);

            _sentToday++;
            return (true, _sentToday, limit);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(int SentToday, int Limit)> GetStatusAsync(int limit)
    {
        await _lock.WaitAsync();
        try
        {
            DateOnly today = DateOnly.FromDateTime(PeruClock.Now);
            if (today != _currentDay) return (0, limit);
            return (_sentToday, limit);
        }
        finally
        {
            _lock.Release();
        }
    }
}

/// <summary>
/// Sends transactional email via Brevo SMTP relay using MailKit.
/// Enforces a configurable daily send limit (default 250) to stay within Brevo's free-tier 300/day cap.
/// Configure in appsettings.json under "Smtp".
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _opts;
    private readonly DailyEmailCounter _counter;

    public SmtpEmailService(IOptions<SmtpOptions> options, DailyEmailCounter counter)
    {
        _opts    = options.Value;
        _counter = counter;
    }

    public bool IsConfigured =>
        _opts.Enabled &&
        !string.IsNullOrWhiteSpace(_opts.Host) &&
        !string.IsNullOrWhiteSpace(_opts.Username) &&
        !string.IsNullOrWhiteSpace(_opts.Password) &&
        !string.IsNullOrWhiteSpace(_opts.FromAddress);

    public async Task<(int SentToday, int Limit)> GetDailyStatusAsync()
        => await _counter.GetStatusAsync(_opts.DailyLimit);

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("SMTP no está configurado. Completa la sección 'Smtp' en appsettings.json.");

        var (allowed, sentToday, limit) = await _counter.TryConsumeAsync(_opts.DailyLimit);
        if (!allowed)
            throw new InvalidOperationException(
                $"Límite diario de correos alcanzado ({limit}/día). Reinicia mañana o aumenta 'Smtp:DailyLimit' en appsettings.json.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opts.FromName, _opts.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body    = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_opts.Host, _opts.Port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_opts.Username, _opts.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
