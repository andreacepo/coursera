namespace EventEaseApp.Models;

public sealed class AttendanceRecord
{
    public int EventId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int AttendeeCount { get; set; }
    public DateTime CheckedInAtUtc { get; set; } = DateTime.UtcNow;
}
