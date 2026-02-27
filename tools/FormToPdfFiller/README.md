# FormToPdfFiller

Utility that reads `output/mapping.json` and `output/data.json`, fills the corresponding AcroForm fields in each source PDF and either flattens the result and merges all documents, or simply writes out each transformed file individually.

## Usage

Run from the workspace root:

```bash
# default behaviour: flatten & merge
dotnet run --project tools/FormToPdfFiller

# save each filled PDF separately (each file is flattened)
dotnet run --project tools/FormToPdfFiller --keep-forms
# (alias: --no-flatten)
```

The tool prints the name of each PDF and the number of fields populated.

## Output modes

- **Flattened & merged (default)** – all forms are flattened and concatenated
  into `output/merged.pdf`.

- **Individual files (`--keep-forms`)** – each source PDF is filled, the form is
  flattened, and the result is written to `output/individual/<originalName>.pdf`.
  No merged document is produced.

The previous implementation attempted to keep forms intact; because the
underlying `PdfSharpCore` library could not properly merge page content while
retaining `/AcroForm` data, that approach produced a file that looked read‑only.

## Flattening notes

The implementation sets `/NeedAppearances` and then removes the `/AcroForm`
entry to flatten the form. This leaves visible appearance streams on the page
and prevents further editing. If you need a merged, interactive PDF, use
another library or an external tool (Adobe Acrobat, `pdftk`, etc.) to combine
`output/individual` files.

## Dependencies

Same as the config generator: uses `PdfSharpCore` and `System.Text.Json`.


