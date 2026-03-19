public class Barcode
{
    public required string Data {get;set;}
    public required BarcodeType Type {get;set;}

    public int? CheckDigit {get;set;}
}
