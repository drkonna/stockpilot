using System.Globalization;
using ClosedXML.Excel;

namespace StockPilot.Api.Services.Import;

public class XlsxProductImportReader : IProductImportReader
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".xlsx"];

    public async Task<IReadOnlyList<ImportRow>> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        // The library needs a seekable stream; an upload stream is not guaranteed to be one
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        using var workbook = Open(ms);

        // Only the first worksheet is read
        var sheet = workbook.Worksheets.FirstOrDefault()
                    ?? throw new ImportFileException("Το αρχείο δεν περιέχει φύλλο εργασίας.");

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow == 0 || lastColumn == 0)
            throw new ImportFileException("Το αρχείο είναι κενό.");

        // Header = row 1. Excel columns are 1-based, our map indexes are 0-based.
        var header = Enumerable.Range(1, lastColumn)
            .Select(c => (string?)sheet.Cell(1, c).GetString())
            .ToList();
        var map = ImportColumns.MapHeader(header);

        var rows = new List<ImportRow>();
        for (var r = 2; r <= lastRow; r++)
        {
            ct.ThrowIfCancellationRequested();

            // r is the real Excel row number, so error messages match what the user sees
            var row = ImportColumns.ToRow(r, map, i => CellText(sheet.Cell(r, i + 1)));
            if (row is not null) rows.Add(row);
        }

        return rows;
    }

    private static XLWorkbook Open(Stream stream)
    {
        try
        {
            return new XLWorkbook(stream);
        }
        catch (Exception)
        {
            // e.g. a renamed .csv, a corrupt file, an old .xls
            throw new ImportFileException("Το αρχείο δεν είναι έγκυρο αρχείο Excel (.xlsx).");
        }
    }

    // Numbers are written with the invariant culture so "12.5" never becomes "12,5"
    // on a machine with Greek regional settings.
    private static string? CellText(IXLCell cell)
    {
        if (cell.DataType == XLDataType.Number)
            return cell.GetDouble().ToString("0.############", CultureInfo.InvariantCulture);

        return cell.GetString();
    }
}