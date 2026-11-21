namespace Wallet.Core.Ledger;

public sealed class LedgerTransaction
{
    public const int DescriptionMaxLength = 250;

    private readonly List<LedgerEntry> _entries = [];

    private LedgerTransaction()
    {
    }

    internal LedgerTransaction(Guid id, TransactionType type, string? description, Guid? reversesTransactionId, DateTimeOffset createdAt)
    {
        Id = id;
        Type = type;
        Description = description;
        ReversesTransactionId = reversesTransactionId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public TransactionType Type { get; private set; }

    public string? Description { get; private set; }

    public Guid? ReversesTransactionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<LedgerEntry> Entries => _entries;

    internal void AddEntry(LedgerEntry entry) => _entries.Add(entry);
}
