using dnotes_backend.Models;

namespace dnotes_backend.Models;

public class Recipient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Relationship { get; set; } // wife, friend, son etc
    public bool IsUnlocked { get; set; } = false;
    public bool IsNotified { get; set; } = false;
    public DateTime? NotifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Message Message { get; set; } = null!;
    public MessageUnlock? MessageUnlock { get; set; }
}