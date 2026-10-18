using System.Text;
using ClosedXML.Excel;
using StockPilot.Api.Services.Import;

namespace StockPilot.Api.Tests;

public class XlsxProductImportReaderTests
{
    private readonly XlsxProductImportReader _reader = new();

    private static MemoryStream Build(Action<IXLWorksheet> fill)
    {
        using var wb = new XLWorkbook();
        fill(wb.AddWorksheet("Products"));

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static void Header(IXLWorksheet ws)
    {
        for (var i = 0; i < ImportColumns.All.Length; i++)
            ws.Cell(1, i + 1).Value = ImportColumns.All[i];
    }

    [Fact]
    public async Task Reads_text_and_numbers_with_excel_row_numbers()
    {
        using var file = Build(ws =>
        {
            Header(ws);
            ws.Cell(2, 1).Value = "A1";
            ws.Cell(2, 2).Value = "Καφές";
            ws.Cell(2, 3).Value = 12.5;   // numeric cell
            ws.Cell(2, 4).Value = 10;     // numeric cell
            ws.Cell(2, 6).Value = "Acme";
        });

        var rows = await _reader.ReadAsync(file);

        var row = Assert.Single(rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("A1", row.Sku);
        Assert.Equal("Καφές", row.Name);
        Assert.Equal("12.5", row.UnitPrice);   // would be "12,5" with Greek regional settings
        Assert.Equal("10", row.Quantity);
        Assert.Null(row.Category);
        Assert.Equal("Acme", row.Supplier);
    }

    [Fact]
    public async Task Headers_are_case_insensitive_and_in_any_order()
    {
        string[] header = ["quantity", "SKU", "name", "unitprice", "category", "supplier", "productfamily", "color", "size"];

        using var file = Build(ws =>
        {
            for (var i = 0; i < header.Length; i++) ws.Cell(1, i + 1).Value = header[i];
            ws.Cell(2, 1).Value = 5;
            ws.Cell(2, 2).Value = "A1";
        });

        var row = Assert.Single(await _reader.ReadAsync(file));
        Assert.Equal("A1", row.Sku);
        Assert.Equal("5", row.Quantity);
    }

    [Fact]
    public async Task Blank_rows_are_skipped_but_row_numbers_stay_true()
    {
        using var file = Build(ws =>
        {
            Header(ws);
            ws.Cell(2, 1).Value = "A1";
            ws.Cell(4, 1).Value = "A2";   // row 3 left blank
        });

        var rows = await _reader.ReadAsync(file);

        Assert.Equal([2, 4], rows.Select(r => r.RowNumber));
    }

    [Fact]
    public async Task Missing_column_throws_file_error_naming_it()
    {
        using var file = Build(ws =>
        {
            var columns = ImportColumns.All.Where(c => c != "Supplier").ToArray();
            for (var i = 0; i < columns.Length; i++) ws.Cell(1, i + 1).Value = columns[i];
        });

        var ex = await Assert.ThrowsAsync<ImportFileException>(() => _reader.ReadAsync(file));
        Assert.Contains("Supplier", ex.Message);
    }

    [Fact]
    public async Task Empty_sheet_throws_file_error()
    {
        using var file = Build(_ => { });

        await Assert.ThrowsAsync<ImportFileException>(() => _reader.ReadAsync(file));
    }

    [Fact]
    public async Task Non_xlsx_content_throws_file_error()
    {
        using var notExcel = new MemoryStream(Encoding.UTF8.GetBytes("Sku,Name\nA1,x\n"));

        await Assert.ThrowsAsync<ImportFileException>(() => _reader.ReadAsync(notExcel));
    }
}