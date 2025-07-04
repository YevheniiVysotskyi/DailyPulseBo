public class UserState
{
    public long ChatId { get; set; }
    public string CurrentStep { get; set; } = "main";
    public Event? CurrentEvent { get; set; }
    public List<string> SelectedTags { get; set; } = new();
}