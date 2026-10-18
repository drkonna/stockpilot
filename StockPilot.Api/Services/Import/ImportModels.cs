namespace StockPilot.Api.Services.Import;

// One data row as read from a file, before any validation.
// Everything is text on purpose: the readers only "translate" a file format
// into this shape; parsing numbers and checking rules happens later, in one place.
public record ImportRow(
    int RowNumber,
    string? Sku,
    string? Name,
    string? UnitPrice,
    string? Quantity,
    string? Category,
    string? Supplier,
    string? ProductFamily,
    string? Color,
    string? Size
    );

// One entry per failed row; several problems in the same row are joined in Message.
public record ImportError(int Row, string? Sku, string Message);

public class ImportResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public List<ImportError> Errors { get; } = new();
    public int Failed => Errors.Count;
}

// Target interface of the Adapter pattern: every file format gets its own
// reader, and all of them produce the same ImportRow list.
public interface IProductImportReader
{
    // File extensions this reader understands, e.g. ".csv", ".txt".
    IReadOnlyCollection<string> SupportedExtensions { get; }

    Task<IReadOnlyList<ImportRow>> ReadAsync(Stream stream, CancellationToken ct = default);
}

// Thrown when the whole file is unusable (empty, missing required columns...),
// as opposed to a single bad row. The endpoint turns it into a 400.
public class ImportFileException(string message) : Exception(message);

public static class ImportColumns
{
    public static readonly string[] All =
        ["Sku", "Name", "UnitPrice", "Quantity", "Category", "Supplier", "ProductFamily", "Color", "Size"];

    // Maps header cells (any order, any case) to column indexes; all columns must exist.
    public static Dictionary<string, int> MapHeader(IReadOnlyList<string?> headerCells)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headerCells.Count; i++)
        {
            var h = headerCells[i]?.Trim().TrimStart('\uFEFF').Trim();
            if (!string.IsNullOrEmpty(h)) map.TryAdd(h, i); // first occurrence wins
        }

        var missing = All.Where(c => !map.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            throw new ImportFileException($"Λείπουν οι στήλες: {string.Join(", ", missing)}.");

        return map;
    }

    // Builds a row from raw cells; empty cells become null; returns null for a fully blank row.
    public static ImportRow? ToRow(int rowNumber, Dictionary<string, int> map, Func<int, string?> cell)
    {
        string? Get(string column)
        {
            var value = cell(map[column])?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        var row = new ImportRow(rowNumber,
            Get("Sku"), Get("Name"), Get("UnitPrice"), Get("Quantity"), Get("Category"),
            Get("Supplier"), Get("ProductFamily"), Get("Color"), Get("Size"));

        var blank = row.Sku is null && row.Name is null && row.UnitPrice is null && row.Quantity is null
                    && row.Category is null && row.Supplier is null && row.ProductFamily is null
                    && row.Color is null && row.Size is null;
        return blank ? null : row;
    }
}