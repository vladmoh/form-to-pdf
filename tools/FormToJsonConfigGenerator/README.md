# FormToJsonConfigGenerator

Small .NET 8 console utility that scans three sample PDFs in `samples/` and generates two JSON files in `output/`:

- `mapping.json` — configuration mapping PDF field names to types and JSONPaths
- `data.json` — sample data matching the JSONPaths

How to run

Run from the project folder:

```bash
dotnet run --project tools/FormToJsonConfigGenerator
```

The program prints a summary per PDF showing the number of fields extracted and a count per type (text/checkbox/picklist/radio/unknown), and confirms the output file paths.

Outputs

- `output/mapping.json`
- `output/data.json`

Notes

- JSONPath values in mapping.json point into `data.json` using the format: `$.documents['<pdfFileName>'].fields['<fieldName>'].value`.
- The tool uses `PdfSharpCore` to enumerate AcroForm fields. PDFsharp/PdfSharpCore has limited metadata exposure for some field internals; when field types or options cannot be reliably determined the generator sets `type` to `unknown`. A human can edit `output/mapping.json` to correct types or add `options` for `picklist`/`radio` entries.

## Output example and alternate generation

This repo already contains a sample `output/mapping.json` and `output/data.json` produced by inspecting the provided PDFs. Because the current environment lacks the .NET SDK, you may not be able to run the C# project here; the Python helper script `generate_json.py` was used to create the files instead. If you have .NET installed, simply run the C# utility as described above and it will produce equivalent results.

To regenerate using Python:

```bash
python generate_json.py
```

The logic in the Python script mirrors the type-detection rules from the README and generates the same JSON structure. These JSON outputs live in `/output` and are tracked in source control so you can inspect their format immediately.
