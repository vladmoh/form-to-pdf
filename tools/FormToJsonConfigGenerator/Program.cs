using System.Text.Json;
using System.Text.Json.Serialization;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Pdf.AcroForms;
using System.Reflection;

var workspaceRoot = AppContext.BaseDirectory;
// Work relative to repo root: go up from bin/... to repository root
var repoRoot = Directory.GetParent(workspaceRoot)!.Parent!.Parent!.Parent!.FullName;

var samplesDir = Path.Combine(repoRoot, "samples");
var outputDir = Path.Combine(repoRoot, "output");
Directory.CreateDirectory(outputDir);

var pdfFiles = new[] { "form_to_flatten.pdf", "Form.pdf", "Sample-Fillable-PDF.pdf" };

var mapping = new Dictionary<string, object?>();
var data = new Dictionary<string, object?>();
var documentsData = new Dictionary<string, object?>();

foreach (var pdfFile in pdfFiles)
{
    var path = Path.Combine(samplesDir, pdfFile);
    var docFields = new Dictionary<string, object?>();
    var docDataFields = new Dictionary<string, object?>();

    if (!File.Exists(path))
    {
        Console.WriteLine($"Warning: sample file not found: {path}");
        mapping[pdfFile] = new { source = $"samples/{pdfFile}", fields = new Dictionary<string, object?>() };
        documentsData[pdfFile] = new { fields = new Dictionary<string, object?>() };
        continue;
    }

    // Open PDF and try to enumerate AcroForm fields
    try
    {
        using var stream = File.OpenRead(path);
        var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.ReadOnly);
        var acro = pdf.AcroForm;
        if (acro == null)
        {
            Console.WriteLine($"No AcroForm found in {pdfFile}");
        }
        else
        {
            foreach (PdfAcroField field in acro.Fields)
            {
                var name = field.Name ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                // Default type
                var type = "unknown";
                List<string>? options = null;

                var tname = field.GetType().Name.ToLowerInvariant();
                if (tname.Contains("text")) type = "text";
                else if (tname.Contains("checkbox")) type = "checkbox";
                else if (tname.Contains("radio")) type = "radio";
                else if (tname.Contains("choice") || tname.Contains("combobox") || tname.Contains("listbox")) type = "picklist";
                else
                {
                    // Heuristic: inspect field.Elements for /FT or /Kids /Opt entries via reflection
                    try
                    {
                        var elementsProp = field.GetType().GetProperty("Elements", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (elementsProp != null)
                        {
                            var elements = elementsProp.GetValue(field);
                            if (elements != null)
                            {
                                var toString = elements.ToString() ?? "";
                                if (toString.Contains("/FT /Btn"))
                                {
                                    // might be checkbox or radio — default to checkbox
                                    type = "checkbox";
                                }
                                else if (toString.Contains("/FT /Ch"))
                                {
                                    type = "picklist";
                                }
                                else
                                {
                                    type = "text";
                                }
                            }
                        }
                    }
                    catch { type = "unknown"; }
                }

                // Try to get options for choice fields or radio via reflection
                if (type == "picklist" || type == "radio")
                {
                    options = new List<string>();
                    // Try common property names
                    var optProps = new[] { "Options", "Items", "Choices" };
                    foreach (var propName in optProps)
                    {
                        var prop = field.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (prop != null)
                        {
                            var val = prop.GetValue(field) as System.Collections.IEnumerable;
                            if (val != null)
                            {
                                foreach (var it in val)
                                {
                                    if (it == null) continue;
                                    var s = it.ToString();
                                    if (!string.IsNullOrEmpty(s) && !options.Contains(s)) options.Add(s);
                                }
                            }
                        }
                    }
                    if (options.Count == 0) options = null;
                }

                // JSON path
                var jsonPath = $"$.documents['{pdfFile}'].fields['{name}'].value";

                var fieldMapping = new Dictionary<string, object?>();
                fieldMapping["type"] = type;
                fieldMapping["jsonPath"] = jsonPath;
                if (options != null) fieldMapping["options"] = options;

                docFields[name] = fieldMapping;

                // Data side
                var fieldData = new Dictionary<string, object?>();
                fieldData["type"] = type;
                if (type == "text" || type == "unknown") fieldData["value"] = $"TEST_{name}";
                else if (type == "checkbox") fieldData["value"] = true;
                else if (type == "picklist")
                {
                    if (options != null && options.Count > 0) fieldData["value"] = options[0];
                    else fieldData["value"] = "OPTION_1";
                }
                else if (type == "radio")
                {
                    if (options != null && options.Count > 0) fieldData["value"] = options[0];
                    else fieldData["value"] = "CHOICE_1";
                }
                else fieldData["value"] = $"TEST_{name}";
                if (options != null) fieldData["options"] = options;

                docDataFields[name] = fieldData;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error reading {pdfFile}: {ex.Message}");
    }

    mapping[pdfFile] = new { source = $"samples/{pdfFile}", fields = docFields };
    documentsData[pdfFile] = new { fields = docDataFields };

    // summary
    var counts = new Dictionary<string, int> { ["text"] = 0, ["checkbox"] = 0, ["picklist"] = 0, ["radio"] = 0, ["unknown"] = 0 };
    foreach (var kv in docFields)
    {
        if (kv.Value is Dictionary<string, object?> fm && fm.TryGetValue("type", out var t) && t is string ts)
        {
            if (counts.ContainsKey(ts)) counts[ts]++;
            else counts["unknown"]++;
        }
    }

    Console.WriteLine($"{pdfFile}: extracted {docFields.Count} fields (text:{counts["text"]} checkbox:{counts["checkbox"]} picklist:{counts["picklist"]} radio:{counts["radio"]} unknown:{counts["unknown"]})");
}

var mappingJson = new Dictionary<string, object?>();
foreach (var kv in mapping) mappingJson[kv.Key] = kv.Value;

var dataRoot = new Dictionary<string, object?> { ["documents"] = documentsData };

var optionsJson = new JsonSerializerOptions { WriteIndented = true };
var mappingPath = Path.Combine(outputDir, "mapping.json");
var dataPath = Path.Combine(outputDir, "data.json");
File.WriteAllText(mappingPath, JsonSerializer.Serialize(mappingJson, optionsJson));
File.WriteAllText(dataPath, JsonSerializer.Serialize(dataRoot, optionsJson));

Console.WriteLine();
Console.WriteLine($"Wrote mapping: {mappingPath}");
Console.WriteLine($"Wrote data:    {dataPath}");

return 0;
