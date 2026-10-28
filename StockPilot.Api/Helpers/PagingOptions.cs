using System.ComponentModel.DataAnnotations;

namespace StockPilot.Api.Helpers;

public class PagingOptions
{
    public const string SectionName = "Paging";

    [Range(1, 1000)]
    public int MaxPageSize { get; init; } = 100;
}