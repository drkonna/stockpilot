namespace StockPilot.Api.Models;

// One row per successful import file. Used to reject older files for the same store
// and as an audit trail (who imported what, when, with what outcome).
public class ImportBatch
{
    public int Id { get; set; }

    public int StoreId { get; set; }
    public Store? Store { get; set; }

    // Date taken from the file name ({StoreCode}_DDMMYY)
    public DateOnly FileDate { get; set; }
    public string FileName { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    // Nullable on purpose: the batch is kept if the user is later deleted
    public int? UserId { get; set; }
    public User? User { get; set; }

    public int Added { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
}