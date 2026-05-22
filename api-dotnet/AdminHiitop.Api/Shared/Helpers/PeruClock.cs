namespace AdminHiitop.Api.Shared.Helpers;

public static class PeruClock
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    public static DateTime Now => DateTimeOffset.UtcNow.ToOffset(Offset).DateTime;
    public static DateTime Today => Now.Date;
}
