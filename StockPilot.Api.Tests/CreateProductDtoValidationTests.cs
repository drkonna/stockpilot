using System.ComponentModel.DataAnnotations;
using StockPilot.Api.Dtos;
using Xunit;

namespace StockPilot.Api.Tests;

// These tests don't check a class with custom logic - they check the
// [Required]/[Range]/[StringLength] Data Annotations on CreateProductDto.
// We can't call Program.cs's own TryValidate<T> helper here because it's a
// *local function* declared inside top-level statements, which C# never
// exposes outside that file - so instead we call the same underlying
// engine it wraps: System.ComponentModel.DataAnnotations.Validator.
// That still exercises the real validation rules, just without the wrapper.
public class CreateProductDtoValidationTests
{
    // Runs the DTO through the same validation engine ASP.NET Core itself
    // uses under the hood, and collects every rule that failed.
    private static IList<ValidationResult> Validate(CreateProductDto dto)
    {
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, context, results, validateAllProperties: true);
        return results;
    }

    // A DTO that passes every validation rule. Each test below starts from
    // this and breaks exactly ONE field - that way, if a test fails, we know
    // precisely which rule caused it, instead of guessing among several
    // broken fields at once.
    private static CreateProductDto ValidDto() => new()
    {
        Name = "Μπλούζα",
        Sku = "SKU-001",
        QuantityInStock = 10,
        UnitPrice = 19.99m,
        SupplierId = 1,
    };

    [Fact]
    public void ValidDto_HasNoValidationErrors()
    {
        var dto = ValidDto();

        var results = Validate(dto);

        // Baseline/control case: a fully valid DTO should produce zero errors.
        Assert.Empty(results);
    }

    [Fact]
    public void MissingName_FailsValidation()
    {
        var dto = ValidDto();
        dto.Name = "";

        var results = Validate(dto);

        // [Required] on Name should reject an empty string.
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProductDto.Name)));
    }

    // [Theory] + [InlineData] runs the SAME test method once per data value,
    // instead of copy-pasting a near-identical [Fact] for every case. xUnit
    // reports each InlineData value as its own separate passed/failed test.
    [Theory]
    [InlineData(0)]     // κάτω από το ελάχιστο (0.01)
    [InlineData(-5)]    // αρνητική τιμή
    public void InvalidUnitPrice_FailsValidation(decimal unitPrice)
    {
        var dto = ValidDto();
        dto.UnitPrice = unitPrice;

        var results = Validate(dto);

        // [Range(0.01, double.MaxValue)] on UnitPrice should reject both
        // zero and negative values.
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProductDto.UnitPrice)));
    }

    [Fact]
    public void SupplierId_Zero_FailsValidation()
    {
        var dto = ValidDto();
        dto.SupplierId = 0;

        var results = Validate(dto);

        // [Range(1, int.MaxValue)] on SupplierId - a supplier must actually
        // be selected, 0 is not a valid id.
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProductDto.SupplierId)));
    }

    [Fact]
    public void NegativeQuantity_FailsValidation()
    {
        var dto = ValidDto();
        dto.QuantityInStock = -1;

        var results = Validate(dto);

        // [Range(0, int.MaxValue)] on QuantityInStock - stock can be zero
        // but never negative.
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProductDto.QuantityInStock)));
    }
}
