using Famick.HomeManagement.Plugin.Abstractions;

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

    public string AsDigits(bool withCheckDigit = true)
    {
        var value = Data;

        if (Type == BarcodeType.GTIN14 && 
            PackagingIndicator.HasValue && 
            withCheckDigit &&
            Data.Length == 12)
        {
            value = PackagingIndicator.ToString() + Data;
        }

        if (withCheckDigit)
        {
            value += CheckDigit.ToString();
        }

        return value;
    }

    public override string ToString()
    {
        var value = CheckDigit.HasValue ? $"{Data}{CheckDigit}" : Data;
        var typeName = Type switch
        {
            BarcodeType.UpcA => "UPC-A",
            BarcodeType.UpcE => "UPC-E",
            BarcodeType.Ean13 => "EAN-13",
            BarcodeType.Ean8 => "EAN-8",
            BarcodeType.QrCode => "QR Code",
            BarcodeType.DataMatrix => "Data Matrix",
            BarcodeType.Pdf417 => "PDF-417",
            BarcodeType.GTIN14 => "GTIN-14",
            _ => Type.ToString()
        };

        if (Type == BarcodeType.GTIN14 && PackagingIndicator.HasValue)
        {
            value = PackagingIndicator.ToString() + value;
        }

        return $"{value} ({typeName})";
    }

    public static Barcode? Parse(string barcodeValue)
    {
        return BarcodeParser.Parse(barcodeValue);
    }

    public static bool TryParse(string value, out Barcode? barcode)
    {
        try
        {
            barcode = Parse(value);
            return true;
        }
        catch
        {
            barcode = null;
            return false;
        }
    }

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
