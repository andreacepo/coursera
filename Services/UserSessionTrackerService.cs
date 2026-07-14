using EventEaseApp.Models;

namespace EventEaseApp.Services;

public sealed class UserSessionTrackerService
{
    private readonly Dictionary<int, EventRegistration> draftsByEventId = [];
    private readonly TimeSpan sessionTimeout = TimeSpan.FromMinutes(20);

    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;
    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;
    public string CurrentRoute { get; private set; } = "/";
    public int? SelectedEventId { get; private set; }
    public int InteractionCount { get; private set; }

    public bool IsExpired => DateTime.UtcNow - LastActivityUtc > sessionTimeout;

    public void Touch(string route)
    {
        CurrentRoute = route;
        LastActivityUtc = DateTime.UtcNow;
        InteractionCount++;
    }

    public void SetSelectedEvent(int eventId)
    {
        SelectedEventId = eventId;
        LastActivityUtc = DateTime.UtcNow;
    }

    public void SaveDraft(EventRegistration registration)
    {
        draftsByEventId[registration.EventId] = new EventRegistration
        {
            EventId = registration.EventId,
            FullName = registration.FullName,
            Email = registration.Email,
            Company = registration.Company,
            AttendeeCount = registration.AttendeeCount,
            SpecialRequests = registration.SpecialRequests
        };
        LastActivityUtc = DateTime.UtcNow;
    }

    public EventRegistration? GetDraft(int eventId)
    {
        if (!draftsByEventId.TryGetValue(eventId, out var draft))
        {
            return null;
        }

        return new EventRegistration
        {
            EventId = draft.EventId,
            FullName = draft.FullName,
            Email = draft.Email,
            Company = draft.Company,
            AttendeeCount = draft.AttendeeCount,
            SpecialRequests = draft.SpecialRequests
        };
    }

    public void ClearDraft(int eventId)
    {
        draftsByEventId.Remove(eventId);
        LastActivityUtc = DateTime.UtcNow;
    }
}
