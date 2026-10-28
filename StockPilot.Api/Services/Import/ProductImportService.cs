using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockPilot.Api.Data;
using StockPilot.Api.Models;

namespace StockPilot.Api.Services.Import;

public class ProductImportService(AppDbContext db, IEnumerable<IProductImportReader> readers, IOptions<ImportOptions> options)
{
    private readonly ImportOptions _options = options.Value;

    public async Task<ImportResult> ImportAsync(
        Stream stream, string fileName, int? userId, bool dryRun = false, CancellationToken ct = default)
    {
        // ---- 1. File-level checks: any problem here rejects the whole file ----
        if (!ImportFileInfo.TryParse(fileName, out var parsedInfo, out var error))
            throw new ImportFileException(error);
        var info = parsedInfo!;

        var codeKey = info.StoreCode.ToLower();
        var store = await db.Stores.FirstOrDefaultAsync(s => s.Code.ToLower() == codeKey, ct)
                    ?? throw new ImportFileException($"Δεν υπάρχει κατάστημα με κωδικό '{info.StoreCode}'.");

        var extension = Path.GetExtension(fileName);
        var reader = readers.FirstOrDefault(r => r.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                     ?? throw new ImportFileException(
                         $"Μη υποστηριζόμενος τύπος αρχείου. Επιτρέπονται: {string.Join(", ", readers.SelectMany(r => r.SupportedExtensions))}.");

        // A file older than the last import of this store is rejected; the same day is allowed
        // (quantities are replaced, so re-sending a corrected file is harmless).
        var lastDate = await db.ImportBatches
            .Where(b => b.StoreId == store.Id)
            .MaxAsync(b => (DateOnly?)b.FileDate, ct);
        if (lastDate is not null && info.Date < lastDate.Value)
            throw new ImportFileException(
                $"Το αρχείο ({info.Date:dd/MM/yyyy}) είναι παλαιότερο από την τελευταία εισαγωγή " +
                $"για το κατάστημα {store.Code} ({lastDate.Value:dd/MM/yyyy}).");

        var rows = await reader.ReadAsync(stream, ct);
        if (rows.Count == 0)
            throw new ImportFileException("Το αρχείο δεν περιέχει γραμμές δεδομένων.");
        if (rows.Count > _options.MaxRows)
            throw new ImportFileException($"Το αρχείο έχει πάνω από {_options.MaxRows} γραμμές.");

        // ---- 2. Load everything we need with a few queries (never one query per row) ----
        // Candidates are fetched by lower(Sku); the exact, case-insensitive match is done in memory.
        var skuKeys = rows.Where(r => r.Sku is not null).Select(r => r.Sku!.ToLowerInvariant()).Distinct().ToList();
        var existingProducts = await db.Products
            .Include(p => p.ProductStocks.Where(ps => ps.StoreId == store.Id))
            .Where(p => skuKeys.Contains(p.Sku.ToLower()))
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        var productsBySku = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in existingProducts) productsBySku.TryAdd(p.Sku.Trim(), p); // lowest Id wins if the DB has duplicates

        var categories = ByName(await db.Categories.ToListAsync(ct), c => c.Name);
        var suppliers = ByName(await db.Suppliers.ToListAsync(ct), s => s.Name);
        var families = ByName(await db.ProductFamilies.ToListAsync(ct), f => f.Name);

        var central = store.IsCentral ? store : await db.Stores.FirstAsync(s => s.IsCentral, ct);

        // ---- 3. Process row by row, in memory only ----
        var result = new ImportResult();
        var processed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // SKU -> row that succeeded

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var problems = new List<string>();

            if (row.Sku is null)
                problems.Add("Το SKU (Sku) είναι υποχρεωτικό.");
            else if (row.Sku.Length > 50)
                problems.Add("Το SKU (Sku) δεν μπορεί να ξεπερνά τους 50 χαρακτήρες.");
            else if (processed.TryGetValue(row.Sku, out var firstRow))
                problems.Add($"Το SKU εμφανίζεται ξανά στο αρχείο (πρώτη φορά στη γραμμή {firstRow}).");

            var quantity = 0;
            if (row.Quantity is null
                || !int.TryParse(row.Quantity, NumberStyles.None, CultureInfo.InvariantCulture, out quantity))
                problems.Add("Η ποσότητα (Quantity) πρέπει να είναι ακέραιος αριθμός μεγαλύτερος ή ίσος με 0.");

            decimal? price = null;
            if (row.UnitPrice is not null && TryParsePrice(row.UnitPrice, out var parsedPrice))
                price = parsedPrice;
            const string priceInvalid = "Η τιμή (UnitPrice) πρέπει να είναι αριθμός μεγαλύτερος ή ίσος με 0,01.";

            Product? existing = null;
            if (row.Sku is not null) productsBySku.TryGetValue(row.Sku, out existing);

            if (existing is null)
            {
                // New product: all mandatory fields must be valid.
                if (row.Name is null)
                    problems.Add("Το όνομα (Name) είναι υποχρεωτικό για νέο προϊόν.");
                else if (row.Name.Length is < 2 or > 200)
                    problems.Add("Το όνομα (Name) πρέπει να έχει 2 έως 200 χαρακτήρες.");

                if (row.UnitPrice is null)
                    problems.Add("Η τιμή (UnitPrice) είναι υποχρεωτική για νέο προϊόν.");
                else if (price is null)
                    problems.Add(priceInvalid);

                if (row.Supplier is null)
                    problems.Add("Ο προμηθευτής (Supplier) είναι υποχρεωτικός για νέο προϊόν.");

                problems.AddRange(new[]
                {
                    RelatedNameProblem(row.Supplier, "Ο προμηθευτής", "Supplier"),
                    RelatedNameProblem(row.Category, "Η κατηγορία", "Category"),
                    RelatedNameProblem(row.ProductFamily, "Η οικογένεια προϊόντος", "ProductFamily"),
                }.OfType<string>());

                if (row.Color is { Length: > 50 })
                    problems.Add("Το χρώμα (Color) δεν μπορεί να ξεπερνά τους 50 χαρακτήρες.");
                if (row.Size is { Length: > 50 })
                    problems.Add("Το μέγεθος (Size) δεν μπορεί να ξεπερνά τους 50 χαρακτήρες.");
            }
            else if (store.IsCentral && row.UnitPrice is not null && price is null)
            {
                // Existing product: only the central file may change the price, so only there it is checked.
                problems.Add(priceInvalid);
            }

            if (problems.Count > 0)
            {
                result.Errors.Add(new ImportError(row.RowNumber, row.Sku, string.Join(" ", problems)));
                continue;
            }

            if (existing is not null)
            {
                // Existing SKU: quantity is replaced; price only from the central store; nothing else changes.
                var stock = existing.ProductStocks.FirstOrDefault(ps => ps.StoreId == store.Id);
                if (stock is null)
                    existing.ProductStocks.Add(new ProductStock { StoreId = store.Id, Quantity = quantity });
                else
                    stock.Quantity = quantity;

                if (store.IsCentral && price is not null)
                    existing.UnitPrice = price.Value;

                result.Updated++;
            }
            else
            {
                // Only now (row is valid) are missing categories/suppliers/families created.
                var product = new Product
                {
                    Name = row.Name!,
                    Sku = row.Sku!,
                    UnitPrice = price!.Value,
                    Supplier = GetOrCreate(suppliers, row.Supplier!, n => new Supplier { Name = n }),
                    Category = row.Category is null ? null : GetOrCreate(categories, row.Category, n => new Category { Name = n }),
                    ProductFamily = row.ProductFamily is null ? null : GetOrCreate(families, row.ProductFamily, n => new ProductFamily { Name = n }),
                    Color = row.Color,
                    Size = row.Size,
                    CreatedAt = DateTime.UtcNow
                };
                product.ProductStocks.Add(new ProductStock { StoreId = store.Id, Quantity = quantity });
                // Same rule as the "create product" endpoint: the central store always has a row.
                if (!store.IsCentral)
                    product.ProductStocks.Add(new ProductStock { StoreId = central.Id, Quantity = 0 });

                db.Products.Add(product);
                result.Added++;
            }

            processed[row.Sku!] = row.RowNumber;
        }

        // ---- 4. Save everything at once ----
        if (dryRun)
        {
            db.ChangeTracker.Clear(); // forget all pending changes; nothing was written
            return result;
        }

        if (result.Added + result.Updated > 0)
        {
            var name = Path.GetFileName(fileName);
            db.ImportBatches.Add(new ImportBatch
            {
                StoreId = store.Id,
                FileDate = info.Date,
                FileName = name.Length > 255 ? name[..255] : name,
                UserId = userId,
                Added = result.Added,
                Updated = result.Updated,
                Failed = result.Failed
            });
            // One SaveChanges = one database transaction: either everything is saved or nothing.
            await db.SaveChangesAsync(ct);
        }

        return result;
    }

    private static Dictionary<string, T> ByName<T>(IEnumerable<T> items, Func<T, string> name)
    {
        var dict = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items) dict.TryAdd(name(item).Trim(), item);
        return dict;
    }

    // Names are not unique in the database, so the first match wins; a new one is cached for the rest of the file.
    private static T GetOrCreate<T>(Dictionary<string, T> cache, string name, Func<string, T> create)
    {
        if (!cache.TryGetValue(name, out var entity))
        {
            entity = create(name);
            cache[name] = entity;
        }
        return entity;
    }

    private static string? RelatedNameProblem(string? value, string label, string column) =>
        value is { Length: < 2 or > 100 } ? $"{label} ({column}) πρέπει να έχει 2 έως 100 χαρακτήρες." : null;

    // Accepts 12.50, 12,50, 1.234,50 and 1,234.50. When there is a single separator it is the decimal one.
    // Anything below 0.01 is rejected (same rule as the create-product form).
    private static bool TryParsePrice(string text, out decimal price)
    {
        var s = text.Replace(" ", "").Replace("\u00A0", "");
        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
            s = lastComma > lastDot ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
        else if (lastComma >= 0)
            s = s.Replace(',', '.');

        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                   CultureInfo.InvariantCulture, out price)
               && price >= 0.01m;
    }
}