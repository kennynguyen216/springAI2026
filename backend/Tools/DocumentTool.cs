using System.ComponentModel;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

/// <summary>
/// Provides document-reading tool functions exposed to agents.
/// </summary>
public static class DocumentTool
{
    /// <summary>
    /// Reads text content from a PDF document.
    /// </summary>
    /// <param name="filePath">The file path or filename to resolve.</param>
    /// <returns>The extracted text or an error message.</returns>
    [Description("Reads the text content from a PDF file. Pass either a full path or just a filename like 'homework4.pdf' and it will search the user's Downloads and Desktop folders automatically.")]
    public static string ReadPdf(string filePath)
    {
        var resolvedPath = FileLocator.ResolveFilePath(filePath, ".pdf");

        try
        {
            using var pdf = PdfDocument.Open(resolvedPath);
            var text = string.Join("\n", pdf.GetPages().Select(page => page.Text));

            return string.IsNullOrWhiteSpace(text) ? "The PDF is empty." : text;
        }
        catch (Exception ex)
        {
            return $"Error reading PDF: {ex.Message}";
        }
    }

    /// <summary>
    /// Reads text content from a Word document.
    /// </summary>
    /// <param name="filePath">The file path or filename to resolve.</param>
    /// <returns>The extracted text or an error message.</returns>
    [Description("Reads the text content from a Word (.docx) document. Pass either a full path or just a filename like 'homework4.docx' and it will search the user's Downloads and Desktop folders automatically.")]
    public static string ReadWord(string filePath)
    {
        var resolvedPath = FileLocator.ResolveFilePath(filePath, ".docx");

        try
        {
            using var document = WordprocessingDocument.Open(resolvedPath, false);
            var body = document.MainDocumentPart?.Document.Body;

            return body?.InnerText ?? "The Word document is empty or unreadable.";
        }
        catch (Exception ex)
        {
            return $"Error reading Word doc: {ex.Message}";
        }
    }
}
