using Google.Cloud.Firestore;

namespace Web_Admin_Booking_App.Services;

public sealed class CodeGeneratorService
{
    private readonly FirestoreDb _firestore;

    private static readonly IReadOnlyDictionary<string, CodeConfig> Configs =
        new Dictionary<string, CodeConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["patients"] = new("patients", "PATIENT"),
            ["doctors"] = new("doctors", "DOC"),
            ["staff"] = new("staff", "STAFF"),
            ["appointments"] = new("appointments", "APPOINTMENT"),
            ["payments"] = new("payments", "PAYMENT"),
            ["invoices"] = new("invoices", "INVOICE"),
            ["prescriptions"] = new("prescriptions", "PRESCRIPTION"),
            ["insurance"] = new("insurance", "INSURANCE"),
            ["cancel_requests"] = new("cancel_requests", "CANCEL-REQUEST"),
            ["notifications"] = new("notifications", "NOTIFICATION")
        };

    public CodeGeneratorService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    public Task<string> GenerateNextCodeAsync(
        string entityType,
        CancellationToken cancellationToken = default)
    {
        return _firestore.RunTransactionAsync(
            transaction => GenerateNextCodeAsync(transaction, entityType),
            cancellationToken: cancellationToken);
    }

    public Task<string> GenerateNextCodeAsync(
        Transaction transaction,
        string entityType)
    {
        if (!Configs.TryGetValue(entityType, out var config))
        {
            throw new ArgumentException($"Unknown code entity type: {entityType}.", nameof(entityType));
        }

        return GenerateNextCodeCoreAsync(transaction, config);
    }

    private async Task<string> GenerateNextCodeCoreAsync(
        Transaction transaction,
        CodeConfig config)
    {
        var counterRef = _firestore.Collection("system_counters").Document(config.CounterId);
        var snapshot = await transaction.GetSnapshotAsync(counterRef, CancellationToken.None);
        var prefix = snapshot.Exists && snapshot.ContainsField("prefix")
            ? snapshot.GetValue<string>("prefix")
            : config.Prefix;
        var padding = snapshot.Exists && snapshot.ContainsField("padding")
            ? Convert.ToInt32(snapshot.GetValue<object>("padding"))
            : config.Padding;
        var currentNumber = snapshot.Exists && snapshot.ContainsField("currentNumber")
            ? Convert.ToInt32(snapshot.GetValue<object>("currentNumber"))
            : 0;
        var next = currentNumber + 1;

        transaction.Set(counterRef, new Dictionary<string, object>
        {
            ["prefix"] = string.IsNullOrWhiteSpace(prefix) ? config.Prefix : prefix.Trim(),
            ["padding"] = padding <= 0 ? config.Padding : padding,
            ["currentNumber"] = next,
            ["updatedAt"] = Timestamp.GetCurrentTimestamp()
        }, SetOptions.MergeAll);

        return $"{(string.IsNullOrWhiteSpace(prefix) ? config.Prefix : prefix.Trim())}-{next.ToString().PadLeft(padding <= 0 ? config.Padding : padding, '0')}";
    }

    private sealed record CodeConfig(string CounterId, string Prefix, int Padding = 6);
}
