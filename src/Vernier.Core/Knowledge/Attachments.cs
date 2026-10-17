using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Vernier.Core.Security;

namespace Vernier.Core.Knowledge;

public sealed class AttachmentException(string message) : Exception(message);

/// <summary>What was read from a file the learner attached: its text, and any pictures inside it (raw bytes, not yet resized).</summary>
public sealed record AttachedDocument(string Name, string Text, IReadOnlyList<(string Name, byte[] Data)> Pictures);

/// <summary>
/// Reads a file the learner picked for one question. Slides and documents are unpacked with the
/// standard zip and XML readers; nothing in them is executed. Content is untrusted like any other file.
/// </summary>
public static class AttachmentReader
{
    public const long MaxFileBytes = 40L * 1024 * 1024;
    public const int MaxChars = 60_000;
    public const int MaxPictures = 4;
    private const long MaxPictureBytes = 6L * 1024 * 1024;
    private const long MaxXmlBytes = 20L * 1024 * 1024;

    public static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];
    public static readonly string[] DocumentExtensions =
        [".pptx", ".docx", ".txt", ".md", ".csv", ".tsv", ".json", ".xml", ".html", ".htm", ".tex", ".log", ".yaml", ".yml",
         ".py", ".cs", ".js", ".ts", ".java", ".c", ".cpp", ".h", ".hpp", ".m", ".r", ".sql", ".go", ".rs", ".kt", ".swift"];

    public static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads a slide deck, a Word document or a text file. Pictures are attached with <see cref="IsImage"/> files separately.</summary>
    public static AttachedDocument Read(string path)
    {
        var name = Path.GetFileName(path);
        if (PathGuard.IsSensitive(path)) throw new AttachmentException($"'{name}' looks like a credential file and is never read.");
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!DocumentExtensions.Contains(ext))
            throw new AttachmentException(ext == ".pdf"
                ? "PDF files cannot be read yet. Open the page and ask about the screen instead, or attach slides, a Word document, text or a picture."
                : $"'{ext}' files cannot be attached. Use a picture, .pptx, .docx or a text file.");
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes) throw new AttachmentException($"'{name}' is larger than {MaxFileBytes / (1024 * 1024)} MB.");
            return ext switch
            {
                ".pptx" => Office(path, name, "ppt/slides/slide", "ppt/media/", "Slide"),
                ".docx" => Office(path, name, "word/document", "word/media/", null),
                _ => new AttachedDocument(name, Cap(File.ReadAllText(path)), []),
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or XmlException)
        {
            throw new AttachmentException($"'{name}' could not be read: {e.Message}");
        }
    }

    // Both formats are zip files of XML where text sits in <a:t>/<w:t> runs inside <a:p>/<w:p> paragraphs.
    private static AttachedDocument Office(string path, string name, string partPrefix, string mediaPrefix, string? partLabel)
    {
        using var zip = ZipFile.OpenRead(path);
        var parts = zip.Entries
            .Where(e => e.FullName.StartsWith(partPrefix, StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => Number(e.FullName)).ToList();
        var text = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length > MaxXmlBytes || text.Length >= MaxChars) break;
            if (partLabel is not null) text.Append(partLabel).Append(' ').Append(Number(part.FullName)).AppendLine(":");
            using var stream = part.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            foreach (var paragraph in XDocument.Load(reader).Descendants().Where(e => e.Name.LocalName == "p"))
            {
                var line = string.Concat(paragraph.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value)).Trim();
                if (line.Length > 0) text.AppendLine(line);
            }
            text.AppendLine();
        }
        var pictures = zip.Entries
            .Where(e => e.FullName.StartsWith(mediaPrefix, StringComparison.OrdinalIgnoreCase) && IsImage(e.FullName) && e.Length is > 2048 and <= MaxPictureBytes)
            .OrderBy(e => Number(e.FullName)).Take(MaxPictures)
            .Select(e =>
            {
                using var s = e.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                return (e.Name, ms.ToArray());
            }).ToList();
        return new AttachedDocument(name, Cap(text.ToString().Trim()), pictures);
    }

    private static int Number(string entryName)
    {
        var digits = new string(Path.GetFileNameWithoutExtension(entryName).Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : 0;
    }

    private static string Cap(string text) => text.Length <= MaxChars ? text : text[..MaxChars] + "\n[…cut: the file is longer than Vernier reads]";
}
