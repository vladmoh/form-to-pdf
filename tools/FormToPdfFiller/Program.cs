using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Pdf.AcroForms;

class Program
{
    static int Main(string[] args)
    {
        var keepForms = args.Contains("--keep-forms") || args.Contains("--no-flatten");

        var repoRoot = Directory.GetCurrentDirectory();
        Console.WriteLine($"Using workspace directory: {repoRoot}");
        var outputDir = Path.Combine(repoRoot, "output");
        Directory.CreateDirectory(outputDir);
        if (keepForms)
        {
            var indivDir = Path.Combine(outputDir, "individual");
            Directory.CreateDirectory(indivDir);
            Console.WriteLine("Will save each filled PDF individually under output/individual (flattened)");
        }

        var mappingPath = Path.Combine(repoRoot, "samples", "mapping.json");
        var dataPath = Path.Combine(repoRoot, "samples", "data.json");

        if (!File.Exists(mappingPath) || !File.Exists(dataPath))
        {
            Console.WriteLine("mapping.json or data.json not found in samples/. Run the config generator first.");
            return 1;
        }

        using var mappingDoc = JsonDocument.Parse(File.ReadAllText(mappingPath));
        using var dataDoc = JsonDocument.Parse(File.ReadAllText(dataPath));

        // document that will hold flattened pages (only used when !keepForms)
        var merged = new PdfDocument();

        var documentsElement = dataDoc.RootElement.GetProperty("documents");

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

            var pdf = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);
            int filledCount = 0;

            if (pdf.AcroForm != null)
            {
                // request that appearances are generated
                pdf.AcroForm.Elements.SetBoolean("/NeedAppearances", true);

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

                    var field = pdf.AcroForm.Fields[fieldName];
                    if (field == null)
                        continue;

                    switch (field)
                    {
                        case PdfTextField tf:
                            tf.Value = new PdfString(value);
                            break;
                        case PdfCheckBoxField cb:
                            if (bool.TryParse(value, out bool b))
                            {
                                cb.Checked = b;
                            }
                            else if (!string.IsNullOrEmpty(value))
                            {
                                if (!value.StartsWith("/"))
                                    value = "/" + value;
                                try { cb.Value = new PdfName(value); } catch { }
                            }
                            break;
                        case PdfRadioButtonField rb:
                            if (!string.IsNullOrEmpty(value))
                            {
                                if (!value.StartsWith("/"))
                                    value = "/" + value;
                                try { rb.Value = new PdfName(value); } catch { }
                            }
                            break;
                        case PdfChoiceField ch:
                            ch.Value = new PdfString(value);
                            break;
                        default:
                            try { field.Value = new PdfString(value); } catch { }
                            break;
                    }

                    filledCount++;
                }

                if (keepForms)
                {
                    // flatten this specific document before saving
                    pdf.Internals.Catalog.Elements.Remove("/AcroForm");
                    // drop any page annotations so forms are truly non-interactive
                    foreach (var page in pdf.Pages)
                        page.Annotations.Clear();
                    var outPath = Path.Combine(outputDir, "individual", pdfName);
                    pdf.Save(outPath);
                }

                if (!keepForms)
                {
                    // flatten by removing the AcroForm dictionary; values remain as appearances
                    pdf.Internals.Catalog.Elements.Remove("/AcroForm");
                    foreach (var page in pdf.Pages)
                        page.Annotations.Clear();
                }
            }

            Console.WriteLine($"{pdfName}: filled {filledCount} fields from data");

            if (!keepForms)
            {
                // append pages: open an import copy so PdfSharpCore allows adding pages
                using (var tempStream = new MemoryStream())
                {
                    pdf.Save(tempStream);
                    tempStream.Position = 0;
                    using var importDoc = PdfReader.Open(tempStream, PdfDocumentOpenMode.Import);
                    foreach (var page in importDoc.Pages)
                        merged.AddPage(page);
                }
            }
        }

        if (!keepForms)
        {
            var mergedPath = Path.Combine(outputDir, "merged.pdf");
            merged.Save(mergedPath);
            Console.WriteLine($"Flattened merged PDF saved to {mergedPath}");
        }
        else
        {
            Console.WriteLine("Each filled PDF has been flattened and saved under output/individual; no merged file was created.");
        }
        return 0;
    }
}
