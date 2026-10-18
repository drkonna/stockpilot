using System.Globalization;
using System.Text.RegularExpressions;

namespace StockPilot.Api.Services.Import;

// Which store and which day an import file is for, taken from its name:
// {StoreCode}_{DDMMYY}.ext  (e.g. MAIN_141026.xlsx)
public record ImportFileInfo(string StoreCode, DateOnly Date)
{
    // The date is the 6 digits after the LAST underscore, so a store code
    // is allowed to contain underscores itself.
    private static readonly Regex Pattern = new(@"^(?<code>.+)_(?<date>\d{6})$", RegexOptions.Compiled);

    public static bool TryParse(string fileName, out ImportFileInfo? info, out string error)
    {
        info = null;
        var name = Path.GetFileNameWithoutExtension(fileName);
        var match = Pattern.Match(name);
        if (!match.Success)
        {
            error = "Το όνομα αρχείου πρέπει να έχει τη μορφή {Κωδικός_Καταστήματος}_ΗΗΜΜΕΕ, π.χ. MAIN_141026.";
            return false;
        }

        if (!DateOnly.TryParseExact(match.Groups["date"].Value, "ddMMyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            error = "Η ημερομηνία στο όνομα του αρχείου δεν είναι έγκυρη (ΗΗΜΜΕΕ).";
            return false;
        }

        info = new ImportFileInfo(match.Groups["code"].Value.Trim(), date);
        error = "";
        return true;
    }
}