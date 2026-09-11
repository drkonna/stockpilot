namespace StockPilot.Api.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Navigation property: η "πολλά" πλευρά της σχέσης
    public List<Product> Products { get; set; } = new();
}