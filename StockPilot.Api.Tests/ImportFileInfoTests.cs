using StockPilot.Api.Services.Import;
using Xunit;

namespace StockPilot.Api.Tests;

// Pure unit tests: no database and no HTTP, just the file-name parser.
public class ImportFileInfoTests
{
    [Theory]
    [InlineData("MAIN_141026.xlsx", "MAIN", 2026, 10, 14)]
    [InlineData("ATH_01_010125.csv", "ATH_01", 2025, 1, 1)]   // underscore inside the store code
    [InlineData("shop2_311225.txt", "shop2", 2025, 12, 31)]
    public void TryParse_WithValidName_ReturnsCodeAndDate(
        string fileName, string code, int year, int month, int day)
    {
        var ok = ImportFileInfo.TryParse(fileName, out var info, out _);

        Assert.True(ok);
        Assert.Equal(code, info!.StoreCode);
        Assert.Equal(new DateOnly(year, month, day), info.Date);
    }

    [Theory]
    [InlineData("products.xlsx")]        // no store code / date
    [InlineData("MAIN_14102026.xlsx")]   // 8 digits instead of 6
    [InlineData("MAIN_321026.xlsx")]     // day 32
    [InlineData("MAIN_141326.xlsx")]     // month 13
    [InlineData("_141026.xlsx")]         // empty store code
    public void TryParse_WithInvalidName_ReturnsFalseWithMessage(string fileName)
    {
        var ok = ImportFileInfo.TryParse(fileName, out var info, out var error);

        Assert.False(ok);
        Assert.Null(info);
        Assert.False(string.IsNullOrEmpty(error));
    }
}