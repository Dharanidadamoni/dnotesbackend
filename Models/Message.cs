using dnotes_backend.Models;

namespace dnotes_backend.Models;

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// AES-256 encrypted body — encrypted on FRONTEND before sending
    /// </summary>
    public string EncryptedBody { get; set; } = string.Empty;

    /// <summary>
    /// "immediate" | "birthday" | "specific"
    /// </summary>
    public string DeliveryType { get; set; } = DeliveryTypes.Immediate;

    /// <summary>
    /// AES-256 encrypted delivery date — even we cannot read it directly
    /// For "birthday": stores encrypted recipient birthday (MM-DD)
    /// For "specific": stores encrypted ISO date (YYYY-MM-DD)
    /// </summary>
    public string? EncryptedDeliveryDate { get; set; }

    /// <summary>
    /// "text" | "audio" | "video"
    /// </summary>
    public string MessageType { get; set; } = MessageTypes.Text;

    /// <summary>
    /// For audio/video: encrypted URL to file stored in blob storage
    /// </summary>
    public string? EncryptedMediaUrl { get; set; }

    /// <summary>
    /// Duration in seconds (for audio/video)
    /// </summary>
    public int? MediaDurationSeconds { get; set; }

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
    public const string Immediate = "immediate";
    public const string Birthday = "birthday";
    public const string Specific = "specific";
}

public static class MessageTypes
{
    public const string Text = "text";
    public const string Audio = "audio";
    public const string Video = "video";
}