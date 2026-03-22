using dnotes_backend.Models;


public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string EncryptedBody { get; set; } = string.Empty; // AES-256 encrypted
    public string DeliveryType { get; set; } = DeliveryTypes.Immediate;
    public string? EncryptedDeliveryDate { get; set; } // encrypted if specific date
    public bool IsDelivered { get; set; } = false;
    public bool IsDraft { get; set; } = true;
    public int WordCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }

    // Navigation
    public User Sender { get; set; } = null!;
    public ICollection<Recipient> Recipients { get; set; } = new List<Recipient>();
}

public static class DeliveryTypes
{
    public const string Immediate = "immediate";   // on death
    public const string Birthday = "birthday";    // recipient's birthday
    public const string Specific = "specific";    // specific date
}