namespace Homeji.Domain.Entities;

public sealed class BadWord
{
    private BadWord()
    {
        Value = null!;
    }

    public BadWord(string value)
    {
        Id = Guid.NewGuid();
        Value = value.Trim().ToLowerInvariant();
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Value { get; private set; }

    public bool IsActive { get; private set; }
}
