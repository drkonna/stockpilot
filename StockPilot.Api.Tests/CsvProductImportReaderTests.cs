using System.Text;
using StockPilot.Api.Services.Import;

namespace StockPilot.Api.Tests;

public class CsvProductImportReaderTests
{
    private const string Header = "Sku,Name,UnitPrice,Quantity,Category,Supplier,ProductFamily,Color,Size";
    private readonly CsvProductImportReader _reader = new();

    private Task<IReadOnlyList<ImportRow>> Read(string content, Encoding? encoding = null, bool bom = false)
    {
        var bytes = (encoding ?? new UTF8Encoding(false)).GetBytes(content);
        if (bom) bytes = [0xEF, 0xBB, 0xBF, .. bytes];
        return _reader.ReadAsync(new MemoryStream(bytes));
    }

    [Fact]
    public async Task Comma_separated_file_is_mapped_with_file_row_numbers()
    {
        var rows = await Read($"{Header}\nA1,Coffee,12.50,10,Drinks,Acme,Hot,Brown,L\n");

        var row = Assert.Single(rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("A1", row.Sku);
        Assert.Equal("12.50", row.UnitPrice); // reader never parses numbers
        Assert.Equal("L", row.Size);
    }

    [Fact]
    public async Task Semicolon_and_tab_delimiters_are_detected()
    {
        var semi = await Read(Header.Replace(',', ';') + "\nA1;Coffee;12,50;10;;Acme;;;\n");
        Assert.Equal("12,50", Assert.Single(semi).UnitPrice);

        var tab = await Read(Header.Replace(',', '\t') + "\nA1\tCoffee\t12.50\t10\t\tAcme\t\t\t\n");
        Assert.Equal("Coffee", Assert.Single(tab).Name);
    }

    [Fact]
    public async Task Headers_are_case_insensitive_and_in_any_order()
    {
        var rows = await Read("quantity,SKU,name,unitprice,category,supplier,productfamily,color,size\n5,A1,Coffee,1,,,,,\n");

        var row = Assert.Single(rows);
        Assert.Equal("A1", row.Sku);
        Assert.Equal("5", row.Quantity);
    }

    [Fact]
    public async Task Empty_cells_and_short_rows_become_null()
    {
        var rows = await Read($"{Header}\nA1,,,7\n");

        var row = Assert.Single(rows);
        Assert.Null(row.Name);
        Assert.Equal("7", row.Quantity);
        Assert.Null(row.Supplier);
        Assert.Null(row.Size);
    }

    [Fact]
    public async Task Blank_lines_are_skipped_but_row_numbers_stay_true()
    {
        var rows = await Read($"{Header}\n\nA1,Coffee,1,1,,Acme,,,\n");

        Assert.Equal(3, Assert.Single(rows).RowNumber);
    }

    [Fact]
    public async Task Utf8_with_bom_keeps_greek_text()
    {
        var rows = await Read($"{Header}\nA1,Καφές,1,1,,Acme,,,\n", bom: true);

        Assert.Equal("Καφές", Assert.Single(rows).Name);
    }

    [Fact]
    public async Task Windows1253_file_falls_back_and_keeps_greek_text()
    {
        var greek = CodePagesEncodingProvider.Instance.GetEncoding(1253)!;
        var rows = await Read($"{Header}\nA1,Καφές,1,1,,Acme,,,\n", greek);

        Assert.Equal("Καφές", Assert.Single(rows).Name);
    }

    [Fact]
    public async Task Missing_column_throws_file_error_naming_it()
    {
        var ex = await Assert.ThrowsAsync<ImportFileException>(() =>
            Read("Sku,Name,UnitPrice,Quantity,Category,ProductFamily,Color,Size\nA1,x,1,1,,,,\n"));

        Assert.Contains("Supplier", ex.Message);
    }

    [Fact]
    public async Task Empty_file_throws_file_error()
    {
        await Assert.ThrowsAsync<ImportFileException>(() => Read(""));
    }
}