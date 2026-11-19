using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;

namespace Paperless.Infrastructure.Ocr;

public sealed class TesseractOcrEngine(IOptions<OcrOptions> options, ILogger<TesseractOcrEngine> logger) : IOcrEngine
{
    public async Task<OcrExtraction> ExtractAsync(byte[] original, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        var directory = Path.Combine(Path.GetTempPath(), $"paperless-ocr-{Guid.NewGuid():N}");
        try
        {
            var format = DetectFormat(original);
            if (format != "pdf") ValidateImage(original, format);
            Directory.CreateDirectory(directory);
            var input = Path.Combine(directory, $"original.{format}");
            await File.WriteAllBytesAsync(input, original, timeout.Token);
            var pages = format == "pdf" ? await GetPageCountAsync(input, timeout.Token) : 1;
            var text = new StringBuilder();
            for (var page = 1; page <= pages; page++)
            {
                var image = input;
                if (format == "pdf")
                {
                    var prefix = Path.Combine(directory, "page");
                    await RequireSuccessAsync(options.Value.PdfToPpmPath,
                        ["-f", page.ToString(CultureInfo.InvariantCulture), "-l", page.ToString(CultureInfo.InvariantCulture),
                         "-singlefile", "-scale-to", options.Value.RenderDimension.ToString(CultureInfo.InvariantCulture), "-gray", "-png", input, prefix], timeout.Token);
                    image = prefix + ".png";
                }
                var output = Path.Combine(directory, "recognized");
                await RequireSuccessAsync(options.Value.TesseractPath,
                    [image, output, "-l", options.Value.Languages, "--oem", "1", "--psm", "3", "txt"], timeout.Token);
                if (page > 1) text.Append("\n\n");
                await AppendTextAsync(output + ".txt", text, timeout.Token);
                File.Delete(output + ".txt");
                if (format == "pdf") File.Delete(image);
            }
            return new OcrExtraction(text.ToString().Replace("\0", string.Empty).Trim(), pages, options.Value.Languages);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OcrException("OcrTimeout", "OCR exceeded its configured time limit. The job can be retried within the attempt limit.", true);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException)
        {
            throw new OcrException("OcrUnavailable", "The OCR tools or temporary storage are currently unavailable.", true, exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new OcrException("InvalidDocument", "The OCR tool produced an unreadable text result.", false, exception);
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Could not remove OCR temporary directory {Directory} ({FailureType}).", directory, exception.GetType().Name);
            }
        }
    }

    internal async Task ValidateDependenciesAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var languages = await RequireSuccessAsync(options.Value.TesseractPath, ["--list-langs"], timeout.Token);
        var installed = languages.Split('\n').Select(line => line.Trim()).ToHashSet(StringComparer.Ordinal);
        if (options.Value.Languages.Split('+').Any(language => !installed.Contains(language)))
            throw new InvalidOperationException("Configured OCR languages are not installed. Install their Tesseract language data or change Ocr:Languages.");
        await RequireSuccessAsync(options.Value.PdfInfoPath, ["-v"], timeout.Token);
        await RequireSuccessAsync(options.Value.PdfToPpmPath, ["-v"], timeout.Token);
        logger.LogInformation("OCR tools are available; languages {Languages}, page limit {MaxPages}, timeout {TimeoutSeconds}s.",
            options.Value.Languages, options.Value.MaxPages, options.Value.TimeoutSeconds);
    }

    private async Task<int> GetPageCountAsync(string input, CancellationToken cancellationToken)
    {
        var info = await RequireSuccessAsync(options.Value.PdfInfoPath, ["-enc", "UTF-8", input], cancellationToken);
        if (Regex.IsMatch(info, @"^Encrypted:\s+yes\b", RegexOptions.Multiline | RegexOptions.CultureInvariant))
            throw new OcrException("EncryptedDocument", "Encrypted PDFs are not supported for OCR. Upload an unencrypted original.", false);
        // The actual page-count line follows user-controlled metadata such as a multiline title.
        var matches = Regex.Matches(info, @"^Pages:\s+(\d+)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var match = matches.Count > 0 ? matches[^1] : Match.Empty;
        if (!match.Success || !int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var pages) || pages < 1)
            throw new OcrException("InvalidDocument", "The PDF page count could not be determined.", false);
        if (pages > options.Value.MaxPages)
            throw new OcrException("OcrPageLimitExceeded", "The PDF exceeds the configured OCR page limit.", false);
        return pages;
    }

    private async Task AppendTextAsync(string path, StringBuilder target, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        if (stream.Length > 4L * options.Value.MaxTextCharacters)
            throw TextLimitExceeded();
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (target.Length + count > options.Value.MaxTextCharacters) throw TextLimitExceeded();
            target.Append(buffer, 0, count);
        }
        if (target.Length > options.Value.MaxTextCharacters) throw TextLimitExceeded();
    }

    private static OcrException TextLimitExceeded() =>
        new("OcrTextLimitExceeded", "The recognized text exceeds the configured result limit.", false);

    private static string DetectFormat(byte[] bytes)
    {
        var data = bytes.AsSpan();
        if (data.StartsWith("%PDF-"u8)) return "pdf";
        if (data.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "png";
        if (data.StartsWith(new byte[] { 255, 216, 255 })) return "jpg";
        throw new OcrException("UnsupportedDocumentFormat", "OCR supports PDF, PNG and JPEG originals. The uploaded original remains available for download.", false);
    }

    private void ValidateImage(byte[] bytes, string format)
    {
        var dimensions = format == "png" && bytes.Length >= 33 && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)
            ? (Width: (long)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)), Height: (long)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)))
            : ReadJpegDimensions(bytes);
        if (dimensions.Width <= 0 || dimensions.Height <= 0)
            throw new OcrException("InvalidDocument", "The image dimensions could not be read.", false);
        if (dimensions.Width > options.Value.MaxImagePixels || dimensions.Height > options.Value.MaxImagePixels ||
            dimensions.Width * dimensions.Height > options.Value.MaxImagePixels)
            throw new OcrException("OcrImageLimitExceeded", "The original image exceeds the configured OCR pixel limit.", false);
    }

    private static (long Width, long Height) ReadJpegDimensions(byte[] bytes)
    {
        var offset = 2;
        while (offset + 4 <= bytes.Length)
        {
            if (bytes[offset++] != 0xff) break;
            while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
            if (offset >= bytes.Length) break;
            var marker = bytes[offset++];
            if (marker is 0xda or 0xd9) break;
            if (marker is 0xd8 or >= 0xd0 and <= 0xd7 or 0x01) continue;
            if (offset + 2 > bytes.Length) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
            if (length < 2 || offset + length > bytes.Length) break;
            if (marker is >= 0xc0 and <= 0xcf and not (0xc4 or 0xc8 or 0xcc) && length >= 8)
                return (BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 5, 2)), BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 3, 2)));
            offset += length;
        }
        return (0, 0);
    }

    private async Task<string> RequireSuccessAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["LC_ALL"] = "C";
        start.Environment["OMP_THREAD_LIMIT"] = "2";
        using var process = new Process { StartInfo = start };
        process.Start();
        var stdout = DrainAsync(process.StandardOutput, cancellationToken);
        var stderr = DrainAsync(process.StandardError, cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            var output = await stdout;
            await stderr;
            if (process.ExitCode != 0)
            {
                logger.LogWarning("OCR tool {Tool} failed with exit code {ExitCode}.", Path.GetFileName(executable), process.ExitCode);
                throw new OcrException("InvalidDocument", "The document could not be read by the OCR tools. Check whether it is damaged or password-protected.", false);
            }
            if (output.Truncated)
                throw new OcrException("InvalidDocument", "The OCR tool output exceeds the supported metadata limit.", false);
            return output.Text;
        }
        catch
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
                using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(shutdown.Token);
            }
            // Observe both readers even when cancellation or native execution failed.
            try { await Task.WhenAll(stdout, stderr); }
            catch (OperationCanceledException) { }
            throw;
        }
    }

    private static async Task<(string Text, bool Truncated)> DrainAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var captured = new StringBuilder();
        var buffer = new char[4096];
        var truncated = false;
        int count;
        while ((count = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            var remaining = 65_536 - captured.Length;
            if (count > remaining) truncated = true;
            if (remaining > 0) captured.Append(buffer, 0, Math.Min(count, remaining));
        }
        return (captured.ToString(), truncated);
    }
}
