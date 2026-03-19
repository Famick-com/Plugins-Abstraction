using Famick.HomeManagement.Plugin.Abstractions;

namespace Famick.HomeManagement.Plugin.Abstractions.Tests;

public class BarcodeParserTests
{
    // ── UPC-A (12 digits with valid check digit) ──

    [Theory]
    [InlineData("042100005264")]   // Cheerios
    [InlineData("761720051108")]   // typical UPC-A
    public void Parse_UpcA_12Digits_WithValidCheck(string barcode)
    {
        var result = BarcodeParser.Parse(barcode);

        Assert.Equal(BarcodeType.UpcA, result.Type);
        Assert.Equal(barcode, result.Data);
        Assert.NotNull(result.CheckDigit);
        Assert.Equal(barcode[^1] - '0', result.CheckDigit);
    }

    [Fact]
    public void Parse_UpcA_11Digits_NoCheckDigit()
    {
        var result = BarcodeParser.Parse("04210000526");

        Assert.Equal(BarcodeType.UpcA, result.Type);
        Assert.Equal("04210000526", result.Data);
        Assert.Null(result.CheckDigit);
    }

    [Fact]
    public void Parse_UpcA_12Digits_InvalidCheck_NullCheckDigit()
    {
        // Swap last digit so check is invalid
        var result = BarcodeParser.Parse("042100005260");

        Assert.Equal(BarcodeType.UpcA, result.Type);
        Assert.Null(result.CheckDigit);
    }

    // ── EAN-13 (13 digits, non-US) ──

    [Fact]
    public void Parse_Ean13_NonUs_WithValidCheck()
    {
        var result = BarcodeParser.Parse("4006381333931");

        Assert.Equal(BarcodeType.Ean13, result.Type);
        Assert.Equal("4006381333931", result.Data);
        Assert.Equal(1, result.CheckDigit);
    }

    [Fact]
    public void Parse_Ean13_NonUs_InvalidCheck_NullCheckDigit()
    {
        var result = BarcodeParser.Parse("4006381333930");

        Assert.Equal(BarcodeType.Ean13, result.Type);
        Assert.Null(result.CheckDigit);
    }

    // ── 13-digit US barcodes (leading 0) → UPC-A ──

    [Fact]
    public void Parse_13Digit_UsEan13_StandardFormat_ExtractsUpcA()
    {
        // 0123456789012 — valid EAN-13 starting with "01" (not "00")
        // Standard EAN-13 path → core = digits[1..12] = 12345678901
        var result = BarcodeParser.Parse("0123456789012");

        Assert.Equal(BarcodeType.UpcA, result.Type);
        Assert.Equal("12345678901", result.Data);
        Assert.Equal(2, result.CheckDigit);
    }

    [Fact]
    public void Parse_13Digit_Kroger_WithoutCheckDigit_ExtractsUpcA()
    {
        // 0007265555627 — Kroger left-padded, NO embedded check digit
        // Right 11 = 07265555627, calculated check = 0
        var result = BarcodeParser.Parse("0007265555627");

        Assert.Equal(BarcodeType.UpcA, result.Type);
        Assert.Equal("07265555627", result.Data);
        Assert.Equal(0, result.CheckDigit);
    }

    [Fact]
    public void Parse_13Digit_Kroger_WithCheckDigit_ExtractsUpcA()
    {
        // 0072655556270 — Kroger left-padded WITH embedded check digit (0)
        // Positions 3-12 calculate check = 0, matches last digit
        // Core = positions 2-12 = 07265555627, check = 0
        var result = BarcodeParser.Parse("0072655556270");

        Assert.Equal(BarcodeType.UpcA, result.Type);
        Assert.Equal("07265555627", result.Data);
        Assert.Equal(0, result.CheckDigit);
    }

    [Fact]
    public void Parse_13Digit_BothKrogerFormats_ProduceSameUpcA()
    {
        // Kroger with and without check digit for the same product
        // must yield identical Barcode results
        var withoutCheck = BarcodeParser.Parse("0007265555627");
        var withCheck = BarcodeParser.Parse("0072655556270");

        Assert.Equal(withoutCheck.Data, withCheck.Data);
        Assert.Equal(withoutCheck.CheckDigit, withCheck.CheckDigit);
        Assert.Equal(withoutCheck.Type, withCheck.Type);
    }

    [Fact]
    public void Parse_13Digit_Kroger_Cheerios_WithoutCheckDigit()
    {
        // Kroger left-padded Cheerios without check digit
        // 00 + 04210000526 = 0004210000526
        var result = BarcodeParser.Parse("0004210000526");

        Assert.Equal(BarcodeType.UpcA, result.Type);
        Assert.Equal("04210000526", result.Data);
        Assert.Equal(4, result.CheckDigit);
    }

    // ── EAN-8 (8 digits with valid check) ──

    [Fact]
    public void Parse_Ean8_WithValidCheck()
    {
        // 96385074 is a valid EAN-8
        var result = BarcodeParser.Parse("96385074");

        Assert.Equal(BarcodeType.Ean8, result.Type);
        Assert.Equal("96385074", result.Data);
        Assert.Equal(4, result.CheckDigit);
    }

    // ── UPC-E (6-8 digits, not valid EAN-8) ──

    [Fact]
    public void Parse_UpcE_6Digits()
    {
        var result = BarcodeParser.Parse("123456");

        Assert.Equal(BarcodeType.UpcE, result.Type);
        Assert.Equal("123456", result.Data);
        Assert.Null(result.CheckDigit);
    }

    [Fact]
    public void Parse_UpcE_7Digits()
    {
        var result = BarcodeParser.Parse("1234567");

        Assert.Equal(BarcodeType.UpcE, result.Type);
        Assert.Equal("1234567", result.Data);
        Assert.NotNull(result.CheckDigit);
        Assert.Equal(7, result.CheckDigit);
    }

    [Fact]
    public void Parse_8Digits_InvalidEan8Check_ReturnUpcE()
    {
        // 12345670 does not have a valid EAN-8 check digit → falls back to UPC-E
        var result = BarcodeParser.Parse("12345670");

        Assert.Equal(BarcodeType.UpcE, result.Type);
        Assert.Equal("12345670", result.Data);
        Assert.Equal(0, result.CheckDigit);
    }

    // ── Non-digit characters stripped ──

    [Fact]
    public void Parse_StripsNonDigitCharacters()
    {
        var result = BarcodeParser.Parse("042-100-005264");

        Assert.Equal(BarcodeType.UpcA, result.Type);
        Assert.Equal("042100005264", result.Data);
    }

    [Fact]
    public void Parse_StripsSpaces()
    {
        var result = BarcodeParser.Parse("0421 0000 5264");

        Assert.Equal(BarcodeType.UpcA, result.Type);
        Assert.Equal("042100005264", result.Data);
    }

    // ── Error cases ──

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullOrEmpty_ThrowsArgumentException(string? value)
    {
        Assert.Throws<ArgumentException>(() => BarcodeParser.Parse(value!));
    }

    [Fact]
    public void Parse_NoDigits_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BarcodeParser.Parse("abc-xyz"));
    }

    [Theory]
    [InlineData("12345")]          // 5 digits
    [InlineData("1234567890")]     // 10 digits
    [InlineData("12345678901234")] // 14 digits
    public void Parse_UnsupportedLength_ThrowsFormatException(string value)
    {
        Assert.Throws<FormatException>(() => BarcodeParser.Parse(value));
    }

    // ── TryParse ──

    [Fact]
    public void TryParse_ValidBarcode_ReturnsTrue()
    {
        var success = BarcodeParser.TryParse("042100005264", out var barcode);

        Assert.True(success);
        Assert.NotNull(barcode);
        Assert.Equal(BarcodeType.UpcA, barcode!.Type);
    }

    [Fact]
    public void TryParse_InvalidBarcode_ReturnsFalse()
    {
        var success = BarcodeParser.TryParse("12345", out var barcode);

        Assert.False(success);
        Assert.Null(barcode);
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse()
    {
        var success = BarcodeParser.TryParse(null!, out var barcode);

        Assert.False(success);
        Assert.Null(barcode);
    }
}
