public class AdminUser
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    public string? UserName { get; set; }
    public bool IsOwner { get; set; }   // тільки власник може керувати списком
}
