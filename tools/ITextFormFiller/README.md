# ITextFormFiller

Small .NET utility that fills AcroForm fields in PDFs based on the
`output/mapping.json` / `output/data.json` files and either merges the
results or writes each filled document individually.  It uses iText 7, which
supports proper flattening of forms.

## Usage

Run from the repository root:

```bash
# flattened/merged output (default)
dotnet run --project tools/ITextFormFiller

# save each filled file separately, still flattened
dotnet run --project tools/ITextFormFiller --keep-forms
# (alias: --no-flatten)
```

The program prints a line per PDF showing how many fields were populated.

## Behavior

- **Default:** all forms are filled and flattened; pages from every source are
  concatenated into `output/merged.pdf`.

- **`--keep-forms`:** each PDF is filled, flattened, and written to
  `output/individual/<name>.pdf`.  No merged file is produced.

Thanks to iText’s `PdfAcroForm.FlattenFields()` method, the filled values are
rendered into the page content, so the resulting documents are read‑only but
still legible.  This overcomes the limitations of the previous `PdfSharpCore`
implementation, which couldn’t generate appearance streams.

## Dependencies

- iText 7 (`itext7` NuGet package)
- Bouncy‑Castle adapter (`itext7.bouncy-castle-adapter`)
- `System.Text.Json` for configuration parsing

(The project is intentionally minimal; feel free to install additional
iText modules as needed.)

## Notes

Dynamic merging of interactive PDFs (i.e. keeping form fields editable after
concatenation) remains tricky; this tool focuses on producing flattened
results.  If you require interactive merging, consider using Adobe Acrobat,
`pdftk`, or another dedicated library.
