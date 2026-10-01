using System.ComponentModel.DataAnnotations;

namespace DeferredTransactions;

public sealed class DeferredTransactionOptions
{
    [Range(typeof(TimeSpan), "00:00:01", "365.00:00:00")]
    public TimeSpan DefaultTimeToLive { get; set; } = TimeSpan.FromMinutes(5);

    [Range(typeof(TimeSpan), "00:00:01", "365.00:00:00")]
    public TimeSpan MaxTimeToLive { get; set; } = TimeSpan.FromMinutes(30);

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

    [Range(typeof(TimeSpan), "00:00:01", "365.00:00:00")]
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);
}
