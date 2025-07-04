public class DailyReport
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string DocId { get; set; } = string.Empty; // Google Docs ID
    public string DocUrl { get; set; } = string.Empty; // повне посилання
    public string Summary { get; set; } = string.Empty; // GPT текст
    public List<int> IncludedEvents { get; set; } = new List<int>();
}