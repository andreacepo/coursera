using System.ComponentModel.DataAnnotations;

namespace EventEaseApp.Models;

public sealed class EventRegistration
{
    public int EventId { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(80, ErrorMessage = "Full name can be up to 80 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Company is required.")]
    [StringLength(120, ErrorMessage = "Company can be up to 120 characters.")]
    public string Company { get; set; } = string.Empty;

    [Range(1, 10, ErrorMessage = "Attendee count must be between 1 and 10.")]
    public int AttendeeCount { get; set; } = 1;

    [StringLength(300, ErrorMessage = "Special requests can be up to 300 characters.")]
    public string SpecialRequests { get; set; } = string.Empty;

    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
}
