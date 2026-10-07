namespace StockPilot.Api.Dtos;

public class StoreResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsCentral { get; set; }
}