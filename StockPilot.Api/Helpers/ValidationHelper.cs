using System.ComponentModel.DataAnnotations;

namespace StockPilot.Api.Helpers;

public static class ValidationHelper
{
    public static bool TryValidate<T>(T model, out IDictionary<string, string[]> errors)
    {
        var context = new ValidationContext(model!);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model!, context, results, validateAllProperties: true);

        errors = results
            .SelectMany(r => r.MemberNames.Select(memberName => new { memberName, r.ErrorMessage }))
            .GroupBy(x => x.memberName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ErrorMessage ?? "Μη έγκυρη τιμή.").ToArray());

        return isValid;
    }
}