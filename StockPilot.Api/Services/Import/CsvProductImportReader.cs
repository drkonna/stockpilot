using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace StockPilot.Api.Services.Import;

public class CsvProductImportReader : IProductImportReader
{
    // Windows-1253 = Greek ANSI encoding, used by older Greek Excel exports
    private static readonly Encoding Greek = CodePagesEncodingProvider.Instance.GetEncoding(1253)!;

    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".csv", ".txt"];

    public async Task<IReadOnlyList<ImportRow>> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);

        var text = Decode(ms.ToArray());
        if (string.IsNullOrWhiteSpace(text))
            throw new ImportFileException("Το αρχείο είναι κενό.");

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = DetectDelimiter(text),
            HasHeaderRecord = false,   // header is read manually so we control matching
            IgnoreBlankLines = false,  // blank lines are skipped by us, so row numbers stay true
            BadDataFound = null,
        };

        using var csv = new CsvReader(new StringReader(text), config);

        if (!await csv.ReadAsync())
            throw new ImportFileException("Το αρχείο είναι κενό.");

        var map = ImportColumns.MapHeader(csv.Parser.Record!);

        var rows = new List<ImportRow>();
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            var record = csv.Parser.Record!;
            var row = ImportColumns.ToRow(csv.Parser.Row, map, i => i < record.Length ? record[i] : null);
            if (row is not null) rows.Add(row);
        }

        return rows;
    }

    // Strict UTF-8 first (BOM skipped); if the bytes are not valid UTF-8, assume Windows-1253.
    private static string Decode(byte[] bytes)
    {
        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            return Greek.GetString(bytes, offset, bytes.Length - offset);
        }
    }

    // Greek Excel writes ';', others ',' or tab: pick the most frequent one in the first line.
    private static string DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n', 2)[0];
        char[] candidates = [';', ',', '\t'];
        var best = candidates.MaxBy(d => firstLine.Count(c => c == d));
        return firstLine.Contains(best) ? best.ToString() : ",";
    }
}