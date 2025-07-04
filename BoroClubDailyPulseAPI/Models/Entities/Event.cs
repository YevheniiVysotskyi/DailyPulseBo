using System.ComponentModel.DataAnnotations;

public class Event
{
    public int Id { get; set; }

    [Required]
    public long ChatId { get; set; }

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string EventType { get; set; } = string.Empty;

    [Required]
    public string PropertyCategory { get; set; } = string.Empty;

    [Required]
    public string PropertyName { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}