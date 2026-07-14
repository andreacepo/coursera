using EventEaseApp.Models;
using System.Text.Json;

namespace EventEaseApp.Services;

public sealed class EventStateService
{
    private static readonly object SyncRoot = new();

    private readonly List<EventInfo> events;
    private readonly List<EventRegistration> registrations = [];
    private readonly List<AttendanceRecord> attendanceRecords = [];
    private readonly string stateFilePath;

    public EventStateService(IWebHostEnvironment environment)
    {
        events = MockEventData.Events
            .Select(eventInfo => new EventInfo
            {
                Id = eventInfo.Id,
                Name = eventInfo.Name,
                Date = eventInfo.Date,
                Location = eventInfo.Location,
                Description = eventInfo.Description
            })
            .ToList();

            var dataDirectory = Path.Combine(environment.ContentRootPath, "AppData");
            Directory.CreateDirectory(dataDirectory);
            stateFilePath = Path.Combine(dataDirectory, "event-state.json");

            LoadStateFromDisk();
    }

    public IReadOnlyList<EventInfo> Events => events;
    public IReadOnlyList<EventRegistration> Registrations => registrations;
    public IReadOnlyList<AttendanceRecord> AttendanceRecords => attendanceRecords;

    public EventInfo? GetEventById(int eventId)
    {
        return events.FirstOrDefault(e => e.Id == eventId);
    }

    public int GetRegistrationCount(int eventId)
    {
        return registrations.Count(r => r.EventId == eventId);
    }

    public int GetAttendanceCount(int eventId)
    {
        return attendanceRecords
            .Where(a => a.EventId == eventId)
            .Sum(a => a.AttendeeCount);
    }

    public bool HasAttendance(int eventId, string email)
    {
        return attendanceRecords.Any(a =>
            a.EventId == eventId
            && string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    public bool TryAddRegistration(EventRegistration input, out string errorMessage)
    {
        var eventInfo = GetEventById(input.EventId);
        if (eventInfo is null)
        {
            errorMessage = "The selected event does not exist.";
            return false;
        }

        var duplicate = registrations.Any(r =>
            r.EventId == input.EventId
            && string.Equals(r.Email, input.Email, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            errorMessage = "This email is already registered for the selected event.";
            return false;
        }

        registrations.Add(new EventRegistration
        {
            EventId = input.EventId,
            FullName = input.FullName,
            Email = input.Email,
            Company = input.Company,
            AttendeeCount = input.AttendeeCount,
            SpecialRequests = input.SpecialRequests,
            RegisteredAtUtc = DateTime.UtcNow
        });

        SaveStateToDisk();

        errorMessage = string.Empty;
        return true;
    }

    public bool TryMarkAttendance(int eventId, string email, out string errorMessage)
    {
        var registration = registrations.FirstOrDefault(r =>
            r.EventId == eventId
            && string.Equals(r.Email, email, StringComparison.OrdinalIgnoreCase));

        if (registration is null)
        {
            errorMessage = "Registration was not found for this participant.";
            return false;
        }

        if (HasAttendance(eventId, email))
        {
            errorMessage = "Participant is already marked as attended.";
            return false;
        }

        attendanceRecords.Add(new AttendanceRecord
        {
            EventId = eventId,
            FullName = registration.FullName,
            Email = registration.Email,
            AttendeeCount = registration.AttendeeCount,
            CheckedInAtUtc = DateTime.UtcNow
        });

        SaveStateToDisk();

        errorMessage = string.Empty;
        return true;
    }

    private void LoadStateFromDisk()
    {
        lock (SyncRoot)
        {
            if (!File.Exists(stateFilePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(stateFilePath);
                var state = JsonSerializer.Deserialize<PersistedState>(json);
                if (state is null)
                {
                    return;
                }

                registrations.Clear();
                registrations.AddRange(state.Registrations);

                attendanceRecords.Clear();
                attendanceRecords.AddRange(state.AttendanceRecords);
            }
            catch
            {
                // Ignore corrupted state and continue with in-memory defaults.
            }
        }
    }

    private void SaveStateToDisk()
    {
        lock (SyncRoot)
        {
            var state = new PersistedState
            {
                Registrations = registrations,
                AttendanceRecords = attendanceRecords
            };

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(stateFilePath, json);
        }
    }

    private sealed class PersistedState
    {
        public List<EventRegistration> Registrations { get; set; } = [];
        public List<AttendanceRecord> AttendanceRecords { get; set; } = [];
    }
}
