using System;
using System.IO;
using System.Text.Json;
using iText.Kernel.Pdf;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Utils;

class Program
{
    static int Main(string[] args)
    {
        bool keepIndividual = args.Contains("--keep-forms") || args.Contains("--no-flatten");

        var workspaceRoot = AppContext.BaseDirectory;
        string repoRoot = FindRepoRoot(workspaceRoot);
        Console.WriteLine($"Using repository root: {repoRoot}");
        var outputDir = Path.Combine(repoRoot, "output");
        Directory.CreateDirectory(outputDir);
        if (keepIndividual)
        {
            var indiv = Path.Combine(outputDir, "individual");
            Directory.CreateDirectory(indiv);
            Console.WriteLine("Will save flattened individual PDFs under output/individual");
        }

        var mappingPath = Path.Combine(outputDir, "mapping.json");
        var dataPath = Path.Combine(outputDir, "data.json");
        if (!File.Exists(mappingPath) || !File.Exists(dataPath))
        {
            Console.WriteLine("mapping.json or data.json not found in output/. Run the config generator first.");
            return 1;
        }

        using var mappingDoc = JsonDocument.Parse(File.ReadAllText(mappingPath));
        using var dataDoc = JsonDocument.Parse(File.ReadAllText(dataPath));
        var documentsElement = dataDoc.RootElement.GetProperty("documents");

        PdfDocument? merged = null;
        PdfMerger? merger = null;
        if (!keepIndividual)
        {
            var mergedPath = Path.Combine(outputDir, "merged.pdf");
            merged = new PdfDocument(new PdfWriter(mergedPath));
            merger = new PdfMerger(merged);
        }

        foreach (var pdfEntry in mappingDoc.RootElement.EnumerateObject())
        {
            var pdfName = pdfEntry.Name;
            var mapObj = pdfEntry.Value;
            var source = mapObj.GetProperty("source").GetString() ?? string.Empty;
            var fieldsElement = mapObj.GetProperty("fields");

            var pdfPath = Path.Combine(repoRoot, source.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(pdfPath))
            {
                Console.WriteLine($"Source PDF not found: {pdfPath}");
                continue;
            }

            int filledCount = 0;
            byte[] filledBytes;
            var tempFile = Path.GetTempFileName();
            try
            {
                // reader from original, writer to temp file
                var pdf = new PdfDocument(new PdfReader(pdfPath), new PdfWriter(tempFile));
                var form = PdfAcroForm.GetAcroForm(pdf, true);
                foreach (var fieldEntry in fieldsElement.EnumerateObject())
                {
                    var fieldName = fieldEntry.Name;
                    if (!documentsElement.TryGetProperty(pdfName, out var docData))
                        continue;
                    if (!docData.GetProperty("fields").TryGetProperty(fieldName, out var fieldData))
                        continue;

                    var valueElement = fieldData.GetProperty("value");
                    string value = valueElement.ValueKind switch
                    {
                        JsonValueKind.String => valueElement.GetString() ?? string.Empty,
                        JsonValueKind.Number => valueElement.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => valueElement.ToString() ?? string.Empty
                    };

                    var field = form.GetField(fieldName);
                    if (field == null)
                        continue;
                    field.SetValue(value);
                    filledCount++;
                }

                form.FlattenFields();
                pdf.Close();
                filledBytes = File.ReadAllBytes(tempFile);
            }
            finally
            {
                File.Delete(tempFile);
            }

            Console.WriteLine($"{pdfName}: filled {filledCount} fields from data");

            if (keepIndividual)
            {
                var outpath = Path.Combine(outputDir, "individual", pdfName);
                File.WriteAllBytes(outpath, filledBytes);
            }

            if (!keepIndividual && merger != null)
            {
                using var srcStream = new MemoryStream(filledBytes);
                using var src = new PdfDocument(new PdfReader(srcStream));
                merger.Merge(src, 1, src.GetNumberOfPages());
            }
        }

        if (merged != null)
        {
            merged.Close();
            Console.WriteLine("Flattened merged PDF saved to output/merged.pdf");
        }

        return 0;
    }

    static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            bool hasToolsFolder = Directory.Exists(Path.Combine(dir.FullName, "tools"));
            bool hasRootScript = File.Exists(Path.Combine(dir.FullName, "fill_and_merge.py")) ||
                                 File.Exists(Path.Combine(dir.FullName, "generate_json.py")) ||
                                 File.Exists(Path.Combine(dir.FullName, "list_fields.py"));
            if (hasToolsFolder && hasRootScript)
                return dir.FullName;
            dir = dir.Parent;
        }
        return start;
    }
}
