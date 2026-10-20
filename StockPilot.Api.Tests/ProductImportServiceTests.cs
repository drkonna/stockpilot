using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StockPilot.Api.Data;
using StockPilot.Api.Models;
using StockPilot.Api.Services.Import;

namespace StockPilot.Api.Tests;

public class ProductImportServiceTests
{
    private const string Header = "Sku,Name,UnitPrice,Quantity,Category,Supplier,ProductFamily,Color,Size";

    // xUnit creates a new instance of this class for every test, so every test gets its own database.
    private readonly string _dbName = Guid.NewGuid().ToString();

    public ProductImportServiceTests()
    {
        using var db = NewDb();
        db.Stores.AddRange(
            new Store { Name = "Central", Code = "MAIN", IsCentral = true },
            new Store { Name = "Athens", Code = "ATH" });
        db.SaveChanges();
    }

    private AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);

    private static string Csv(params string[] rows) => Header + "\n" + string.Join("\n", rows);

    private async Task<ImportResult> Import(string fileName, string csv, bool dryRun = false)
    {
        using var db = NewDb();
        var service = new ProductImportService(db, [new CsvProductImportReader(), new XlsxProductImportReader()]);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await service.ImportAsync(stream, fileName, null, dryRun);
    }

    private async Task<int> Stock(string sku, string storeCode)
    {
        using var db = NewDb();
        return await db.ProductStocks
            .Where(s => s.Product!.Sku == sku && s.Store!.Code == storeCode)
            .Select(s => s.Quantity)
            .SingleAsync();
    }

    [Fact]
    public async Task New_product_from_central_file_creates_product_and_related_entities()
    {
        var result = await Import("MAIN_141026.csv", Csv("A1,Coffee,12.50,10,Drinks,Acme,Hot,Brown,L"));

        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Failed);

        using var db = NewDb();
        var product = await db.Products
            .Include(p => p.Category).Include(p => p.Supplier).Include(p => p.ProductFamily)
            .SingleAsync();
        Assert.Equal("Coffee", product.Name);
        Assert.Equal(12.50m, product.UnitPrice);
        Assert.Equal("Drinks", product.Category!.Name);
        Assert.Equal("Acme", product.Supplier!.Name);
        Assert.Equal("Hot", product.ProductFamily!.Name);
        Assert.Equal("Brown", product.Color);
        Assert.Equal("L", product.Size);
        Assert.Equal(10, await Stock("A1", "MAIN"));
    }

    [Fact]
    public async Task New_product_from_store_file_gets_zero_stock_in_central_store()
    {
        await Import("ATH_141026.csv", Csv("A1,Coffee,12.50,10,,Acme,,,"));

        Assert.Equal(10, await Stock("A1", "ATH"));
        Assert.Equal(0, await Stock("A1", "MAIN"));
    }

    [Fact]
    public async Task Store_code_in_file_name_is_case_insensitive()
    {
        await Import("ath_141026.csv", Csv("A1,Coffee,12.50,4,,Acme,,,"));

        Assert.Equal(4, await Stock("A1", "ATH"));
    }

    [Fact]
    public async Task Existing_sku_in_central_file_updates_quantity_and_price_only()
    {
        await Import("MAIN_141026.csv", Csv("A1,Coffee,12.50,10,Drinks,Acme,,,"));

        var result = await Import("MAIN_151026.csv", Csv("A1,Other name,20,7,Other category,Other supplier,,Red,"));

        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Failed);

        using var db = NewDb();
        var product = await db.Products.Include(p => p.Category).SingleAsync();
        Assert.Equal("Coffee", product.Name);            // differences in other fields are ignored
        Assert.Equal(20m, product.UnitPrice);
        Assert.Equal("Drinks", product.Category!.Name);
        Assert.Null(product.Color);
        Assert.Equal(1, await db.Categories.CountAsync()); // nothing new created for an existing product
        Assert.Equal(7, await Stock("A1", "MAIN"));
    }

    [Fact]
    public async Task Existing_sku_in_store_file_updates_only_that_stores_quantity()
    {
        await Import("MAIN_141026.csv", Csv("A1,Coffee,12.50,10,,Acme,,,"));

        var result = await Import("ATH_151026.csv", Csv("A1,,99,5,,,,,"));

        Assert.Equal(1, result.Updated);
        using var db = NewDb();
        Assert.Equal(12.50m, (await db.Products.SingleAsync()).UnitPrice); // price is ignored outside the central file
        Assert.Equal(5, await Stock("A1", "ATH"));
        Assert.Equal(10, await Stock("A1", "MAIN"));
    }

    [Fact]
    public async Task Sku_matching_ignores_case()
    {
        await Import("MAIN_141026.csv", Csv("A1,Coffee,12.50,10,,Acme,,,"));

        var result = await Import("MAIN_151026.csv", Csv("a1,,,3,,,,,"));

        Assert.Equal(1, result.Updated);
        using var db = NewDb();
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal(3, await Stock("A1", "MAIN"));
    }

    [Fact]
    public async Task Bad_rows_are_reported_and_good_rows_are_still_imported()
    {
        var result = await Import("MAIN_141026.csv", Csv(
            "A1,Coffee,12.50,10,,Acme,,,",
            "B1,Tea,5,3,,,,,",              // new product without supplier
            "C1,Juice,7,2,,Acme,,,"));

        Assert.Equal(2, result.Added);
        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.Row);
        Assert.Equal("B1", error.Sku);
        Assert.Contains("Supplier", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("2.5")]
    public async Task Invalid_quantity_is_a_row_error(string quantity)
    {
        var result = await Import("MAIN_141026.csv", Csv($"A1,Coffee,12.50,{quantity},,Acme,,,"));

        Assert.Equal(0, result.Added);
        Assert.Contains("Quantity", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task Invalid_price_on_new_product_is_a_row_error(string price)
    {
        var result = await Import("MAIN_141026.csv", Csv($"A1,Coffee,{price},1,,Acme,,,"));

        Assert.Equal(0, result.Added);
        Assert.Contains("UnitPrice", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("12.50", "12.50")]
    [InlineData("\"12,50\"", "12.50")]
    [InlineData("\"1.234,50\"", "1234.50")]
    [InlineData("\"1,234.50\"", "1234.50")]
    [InlineData("1234", "1234")]
    public async Task Price_formats_are_understood(string cell, string expected)
    {
        var result = await Import("MAIN_141026.csv", Csv($"A1,Coffee,{cell},1,,Acme,,,"));

        Assert.Equal(1, result.Added);
        using var db = NewDb();
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), (await db.Products.SingleAsync()).UnitPrice);
    }

    [Fact]
    public async Task Duplicate_sku_in_file_errors_on_the_second_row_ignoring_case()
    {
        var result = await Import("MAIN_141026.csv", Csv(
            "A1,Coffee,12.50,1,,Acme,,,",
            "a1,Coffee,12.50,2,,Acme,,,"));

        Assert.Equal(1, result.Added);
        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.Row);
        Assert.Contains("SKU", error.Message);
        Assert.Equal(1, await Stock("A1", "MAIN"));
    }

    [Fact]
    public async Task Failed_row_does_not_block_a_later_valid_row_with_the_same_sku()
    {
        var result = await Import("MAIN_141026.csv", Csv(
            "A1,Coffee,abc,1,,Acme,,,",
            "A1,Coffee,12.50,1,,Acme,,,"));

        Assert.Equal(1, result.Added);
        Assert.Equal(2, Assert.Single(result.Errors).Row);
    }

    [Fact]
    public async Task Related_entities_are_matched_by_name_ignoring_case_and_spaces()
    {
        await Import("MAIN_141026.csv", Csv(
            "A1,Coffee,1,1,Drinks,Acme,,,",
            "A2,Tea,1,1, drinks ,acme,,,"));

        using var db = NewDb();
        Assert.Equal(1, await db.Categories.CountAsync());
        Assert.Equal(1, await db.Suppliers.CountAsync());
    }

    [Fact]
    public async Task Failed_row_does_not_create_related_entities()
    {
        await Import("MAIN_141026.csv", Csv("A1,Coffee,abc,1,Drinks,Acme,,,"));

        using var db = NewDb();
        Assert.Equal(0, await db.Categories.CountAsync());
        Assert.Equal(0, await db.Suppliers.CountAsync());
    }

    [Theory]
    [InlineData("products.csv")]       // no store / date in the name
    [InlineData("XXX_141026.csv")]     // unknown store
    [InlineData("MAIN_141026.pdf")]    // unsupported file type
    public async Task File_level_problems_reject_the_whole_file(string fileName)
    {
        await Assert.ThrowsAsync<ImportFileException>(() =>
            Import(fileName, Csv("A1,Coffee,12.50,1,,Acme,,,")));
    }

    [Fact]
    public async Task Successful_import_is_recorded_and_older_files_for_that_store_are_rejected()
    {
        await Import("MAIN_141026.csv", Csv("A1,Coffee,12.50,10,,Acme,,,"));

        using (var db = NewDb())
        {
            var batch = await db.ImportBatches.SingleAsync();
            Assert.Equal(new DateOnly(2026, 10, 14), batch.FileDate);
            Assert.Equal("MAIN_141026.csv", batch.FileName);
            Assert.Equal(1, batch.Added);
        }

        await Assert.ThrowsAsync<ImportFileException>(() =>
            Import("MAIN_131026.csv", Csv("A1,,,99,,,,,")));
        Assert.Equal(10, await Stock("A1", "MAIN"));

        // The same day is fine, a newer day is fine, and another store has its own history.
        Assert.Equal(1, (await Import("MAIN_141026.csv", Csv("A1,,,11,,,,,"))).Updated);
        Assert.Equal(1, (await Import("MAIN_151026.csv", Csv("A1,,,12,,,,,"))).Updated);
        Assert.Equal(1, (await Import("ATH_131026.csv", Csv("A1,,,1,,,,,"))).Updated);
    }

    [Fact]
    public async Task Dry_run_reports_results_but_saves_nothing()
    {
        var result = await Import("MAIN_141026.csv", Csv("A1,Coffee,12.50,10,Drinks,Acme,,,"), dryRun: true);

        Assert.Equal(1, result.Added);
        using var db = NewDb();
        Assert.Equal(0, await db.Products.CountAsync());
        Assert.Equal(0, await db.Categories.CountAsync());
        Assert.Equal(0, await db.ImportBatches.CountAsync());
    }

    [Fact]
    public async Task File_where_every_row_fails_is_not_recorded()
    {
        var result = await Import("MAIN_141026.csv", Csv("A1,Coffee,abc,1,,Acme,,,"));

        Assert.Equal(1, result.Failed);
        using var db = NewDb();
        Assert.Equal(0, await db.ImportBatches.CountAsync());
    }
}