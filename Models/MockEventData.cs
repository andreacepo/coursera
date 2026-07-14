namespace EventEaseApp.Models;

public static class MockEventData
{
    public static IReadOnlyList<EventInfo> Events { get; } = CreateEvents();

    public static EventInfo? GetById(int id)
    {
        return Events.FirstOrDefault(e => e.Id == id);
    }

    public static EventInfo GetDefault()
    {
        return Events[0];
    }

    private static IReadOnlyList<EventInfo> CreateEvents()
    {
        var baseEvents = new List<EventInfo>
        {
            new()
            {
                Id = 1,
                Name = "Annual Leadership Summit",
                Date = new DateTime(2026, 9, 18),
                Location = "Jakarta Convention Center",
                Description = "Join business leaders, innovators, and professionals for a full-day event of talks, networking, and hands-on sessions."
            },
            new()
            {
                Id = 2,
                Name = "Corporate Innovation Forum",
                Date = new DateTime(2026, 10, 7),
                Location = "Bandung Tech Hub",
                Description = "Explore modern strategies and practical case studies to drive innovation within your organization."
            }
        };

        var countRaw = Environment.GetEnvironmentVariable("EVENTEASE_EVENT_COUNT");
        if (!int.TryParse(countRaw, out var requestedCount) || requestedCount <= baseEvents.Count)
        {
            return baseEvents;
        }

        var generated = new List<EventInfo>(requestedCount);
        for (var i = 0; i < requestedCount; i++)
        {
            var template = baseEvents[i % baseEvents.Count];
            generated.Add(new EventInfo
            {
                Id = i + 1,
                Name = $"{template.Name} #{i + 1}",
                Date = template.Date.AddDays(i % 90),
                Location = template.Location,
                Description = template.Description
            });
        }

        return generated;
    }
}
