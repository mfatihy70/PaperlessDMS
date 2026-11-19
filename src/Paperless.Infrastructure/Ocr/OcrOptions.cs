namespace Paperless.Infrastructure.Ocr;

public sealed class OcrOptions
{
    public string Languages { get; set; } = "deu+eng";
    public string TesseractPath { get; set; } = "tesseract";
    public string PdfInfoPath { get; set; } = "pdfinfo";
    public string PdfToPpmPath { get; set; } = "pdftoppm";
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxPages { get; set; } = 25;
    public int MaxTextCharacters { get; set; } = 1_000_000;
    public int RenderDimension { get; set; } = 3500;
    public int MaxImagePixels { get; set; } = 25_000_000;
}
