namespace dnotes_backend.DTOs;
// ════════════════════════════════════════════════
// DTOs/MessageDtos.cs
// ════════════════════════════════════════════════
using System.ComponentModel.DataAnnotations;



// ── REQUESTS ──────────────────────────────────────
public class CreateMessageRequest
{
    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string EncryptedBody { get; set; } = string.Empty; // pre-encrypted from frontend

    public string DeliveryType { get; set; } = "immediate";

    public string? EncryptedDeliveryDate { get; set; } // encrypted specific date

    public bool IsDraft { get; set; } = true;

    public int WordCount { get; set; } = 0;

    public List<CreateRecipientRequest> Recipients { get; set; } = new();
}

public class UpdateMessageRequest
{
    [MaxLength(500)]
    public string? Title { get; set; }

    public string? EncryptedBody { get; set; }

    public string? DeliveryType { get; set; }

    public string? EncryptedDeliveryDate { get; set; }

    public bool? IsDraft { get; set; }

    public int? WordCount { get; set; }
}

public class CreateRecipientRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Relationship { get; set; }
}

// ── RESPONSES ─────────────────────────────────────
public class MessageDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string EncryptedBody { get; set; } = string.Empty;
    public string DeliveryType { get; set; } = string.Empty;
    public string? EncryptedDeliveryDate { get; set; }
    public bool IsDelivered { get; set; }
    public bool IsDraft { get; set; }
    public int WordCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<RecipientDto> Recipients { get; set; } = new();
}

public class RecipientDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public bool IsUnlocked { get; set; }
    public bool IsNotified { get; set; }
}

public class MessageSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsDelivered { get; set; }
    public bool IsDraft { get; set; }
    public int WordCount { get; set; }
    public int RecipientCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> RecipientNames { get; set; } = new();
}

public class DashboardStatsDto
{
    public int TotalMessages { get; set; }
    public int SavedMessages { get; set; }
    public int DraftMessages { get; set; }
    public int TotalRecipients { get; set; }
    public bool HasVerifier { get; set; }
    public bool IsTriggered { get; set; }
}
