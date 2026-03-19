using System.Text.RegularExpressions;
using Famick.HomeManagement.Core.Interfaces.Plugins;

namespace Famick.HomeManagement.Plugin.Abstractions;

/// <summary>
/// Parses a raw barcode string into a <see cref="Barcode"/> with its detected type and optional check digit.
/// </summary>
public static class BarcodeParser
{
    private static readonly Regex NonDigit = new(@"[^0-9]", RegexOptions.Compiled);

    /// <summary>
    /// Parses a barcode string, detecting its <see cref="BarcodeType"/> based on length and check digit validity.
    /// </summary>
    /// <param name="value">The raw barcode string (may contain non-digit characters).</param>
    /// <returns>A <see cref="Barcode"/> with the detected type, cleaned data, and check digit if applicable.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty, or contains no digits.</exception>
    /// <exception cref="FormatException">Thrown when the digit length does not match any known barcode format.</exception>
    public static Barcode Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Barcode value cannot be null or empty.", nameof(value));

        var digits = NonDigit.Replace(value, "");
        if (digits.Length == 0)
            throw new ArgumentException("Barcode value must contain at least one digit.", nameof(value));

        return digits.Length switch
        {
            6 or 7 or 8 => ParseShortBarcode(digits),
            11 or 12 => ParseUpcA(digits),
            13 => ParseEan13(digits),
            _ => throw new FormatException(
                $"Cannot determine barcode type for {digits.Length}-digit input. " +
                "Expected 6-8 (UPC-E/EAN-8), 11-12 (UPC-A), or 13 (EAN-13) digits.")
        };
    }

    /// <summary>
    /// Tries to parse a barcode string, returning false instead of throwing on failure.
    /// </summary>
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

    private static Barcode ParseShortBarcode(string digits)
    {
        // 6 digits → UPC-E without check digit
        // 7 digits → UPC-E with check digit, or EAN-8 without check digit
        // 8 digits → EAN-8 with check digit, or UPC-E with check digit (disambiguate via check digit validation)
        if (digits.Length == 8 && ProductLookupPipelineContext.HasValidCheckDigit(digits, isEan: true))
        {
            return new Barcode
            {
                Data = digits,
                Type = BarcodeType.Ean8,
                CheckDigit = digits[^1] - '0'
            };
        }

        if (digits.Length <= 8)
        {
            // 6-digit core, possibly with check digit(s)
            int? check = null;
            var data = digits;

            if (digits.Length == 8)
            {
                // 8 digits but not valid EAN-8 → treat as UPC-E with check
                check = digits[^1] - '0';
                data = digits;
            }
            else if (digits.Length == 7)
            {
                // Could be UPC-E with check digit
                check = digits[^1] - '0';
                data = digits;
            }

            return new Barcode
            {
                Data = data,
                Type = BarcodeType.UpcE,
                CheckDigit = check
            };
        }

        // Should not reach here given the caller constrains length to 6-8
        throw new FormatException("Unexpected barcode length.");
    }

    private static Barcode ParseUpcA(string digits)
    {
        if (digits.Length == 12 && ProductLookupPipelineContext.HasValidCheckDigit(digits, isEan: false))
        {
            return new Barcode
            {
                Data = digits,
                Type = BarcodeType.UpcA,
                CheckDigit = digits[^1] - '0'
            };
        }

        // 11 digits (no check digit) or 12 digits without valid check
        return new Barcode
        {
            Data = digits,
            Type = BarcodeType.UpcA,
            CheckDigit = null
        };
    }

    private static Barcode ParseEan13(string digits)
    {
        // 13-digit barcodes starting with '0' are US products stored in EAN-13 format.
        // Extract the 11-digit UPC-A core and return as UPC-A.
        if (digits.StartsWith('0'))
        {
            return ParseUsEan13AsUpcA(digits);
        }

        int? check = null;

        if (ProductLookupPipelineContext.HasValidCheckDigit(digits, isEan: true))
        {
            check = digits[^1] - '0';
        }

        return new Barcode
        {
            Data = digits,
            Type = BarcodeType.Ean13,
            CheckDigit = check
        };
    }

    /// <summary>
    /// Handles 13-digit barcodes starting with '0' (US products).
    /// Extracts the 11-digit UPC-A core and computes the check digit.
    /// </summary>
    /// <remarks>
    /// Kroger and other retailers store barcodes as 13 digits, left-padded with zeros.
    /// Some include the UPC-A check digit, some don't:
    ///   - With check:    0 + 11-digit core + check  (e.g. 0072655556270)
    ///   - Without check: 00 + 11-digit core         (e.g. 0007265555627)
    ///
    /// To distinguish: if the first two digits are "00", take positions 3–12 (10 digits),
    /// calculate a check digit, and compare with the last digit. If they match, the
    /// barcode contains an embedded UPC-A check digit and the core is positions 2–12.
    /// Otherwise the right 11 digits are the core.
    /// </remarks>
    private static Barcode ParseUsEan13AsUpcA(string digits)
    {
        string core;
        int checkDigit;

        if (digits[0] == '0' && digits[1] == '0')
        {
            // First two digits are "00" — likely a left-padded barcode.
            // Check if positions 3–12 (0-indexed: [2..12], 10 digits) calculate
            // a check digit that matches the last digit.
            var innerTen = digits[2..12];
            var calculatedCheck = ProductLookupPipelineContext.CalculateCheckDigit(innerTen);

            if (calculatedCheck == digits[12])
            {
                // Last digit IS a valid check digit for the inner 10 digits.
                // Structure: 0 + 11-digit UPC-A data + check digit
                core = digits[1..12];
                checkDigit = digits[12] - '0';
            }
            else
            {
                // No embedded check digit — right 11 digits are the core.
                // Structure: 00 + 11-digit core
                core = digits[2..];
                checkDigit = ProductLookupPipelineContext.CalculateCheckDigit(core) - '0';
            }
        }
        else
        {
            // Starts with "0" followed by non-zero — standard US EAN-13.
            if (ProductLookupPipelineContext.HasValidCheckDigit(digits, isEan: true))
            {
                core = digits[1..12];
                checkDigit = digits[12] - '0';
            }
            else
            {
                // No valid EAN-13 check digit — treat right 11 as core.
                core = digits[2..];
                checkDigit = ProductLookupPipelineContext.CalculateCheckDigit(core) - '0';
            }
        }

        return new Barcode
        {
            Data = core,
            Type = BarcodeType.UpcA,
            CheckDigit = checkDigit
        };
    }
}
