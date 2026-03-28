public class Barcode : IEquatable<Barcode>
{
    public required string Data { get; set; }
    public required BarcodeType Type { get; set; }

    public int? CheckDigit { get; set; }

    /// <summary>
    /// Packaging indicator digit (1-8) for GTIN-14 barcodes.
    /// Identifies the packaging level (e.g., 1 = inner pack, 2 = multipack).
    /// Null for non-GTIN-14 barcodes or indicator 0 (same as base product).
    /// </summary>
    public int? PackagingIndicator { get; set; }

    public bool Equals(Barcode? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Type == other.Type
            && string.Equals(Data, other.Data, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => Equals(obj as Barcode);

    public override int GetHashCode() =>
        HashCode.Combine(Type, Data?.ToUpperInvariant());

    public static bool operator ==(Barcode? left, Barcode? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Barcode? left, Barcode? right) => !(left == right);
}
