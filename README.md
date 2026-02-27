You are an autonomous coding agent in VS Code. Create a small .NET 8 console utility in C# that generates TWO JSON files for PDF form filling configuration.

Context:
- Folder structure:
  - /samples
    - form_to_flatten.pdf
    - Form.pdf
    - Sample-Fillable-PDF.pdf
- These are NOT XFA forms (standard AcroForm PDFs).
- Goal: prepare configuration for a later app that fills/merges/flattens PDFs.
- I want to try a free library first (PDFsharp or compatible free option).

Goal:
The utility must EXTRACT all AcroForm field names from each PDF in /samples and output:
  1) mapping.json (configuration + field types + JSONPaths)
  2) data.json (structured sample data to populate the fields)

KEY NEW REQUIREMENT:
Mapping must include each field’s "type" as one of:
  - "text"
  - "checkbox"
  - "picklist"
  - "radio"
  - "unknown" (fallback if type cannot be reliably determined)

A) mapping.json format
- Top-level object keys are the PDF file names (exactly the three names above).
- Each PDF name maps to an object with:
  - "source": relative path to the PDF (e.g. "samples/Form.pdf")
  - "fields": object where each key is an extracted field name.
  - For each field name key, the value must be an object:
      {
        "type": "<text|checkbox|picklist|radio|unknown>",
        "jsonPath": "<jsonpath string into data.json>",
        "options": [...]  // only include for picklist and radio when options are discoverable; otherwise omit or empty array
      }

JSONPath convention:
- Use this exact default format:
  $.documents['<pdfFileName>'].fields['<fieldName>'].value

Examples:
- $.documents['Form.pdf'].fields['FirstName'].value
- $.documents['Form.pdf'].fields['AgreeToTerms'].value

B) data.json format
- Must be structured to match the JSONPaths created in mapping.json.
- Top-level shape:
{
  "documents": {
    "<pdfFileName>": {
      "fields": {
        "<fieldName>": {
          "type": "<same as mapping type>",
          "value": <sample value>,
          "options": [...] // only for picklist/radio if you included options in mapping
        }
      }
    }
  }
}

Sample value rules:
- text: "TEST_<fieldName>"
- checkbox: true
- picklist: if options exist, choose the first option; else "OPTION_1"
- radio: if options exist, choose the first option; else "CHOICE_1"
- unknown: "TEST_<fieldName>" (treat unknown as text for the data value)

C) Type detection rules (because PDF type detection is tricky)
- Extract field names for sure.
- Attempt to infer type using AcroForm metadata when available.
- Priority for type detection:
  1) If field is a button:
     - If it behaves like checkbox (single on/off appearance) => "checkbox"
     - If it is part of a group / mutually exclusive => "radio"
  2) If it has a choice list (combo/list) => "picklist"
  3) Otherwise => "text"
- If the library cannot reliably detect any of the above, set type to "unknown" but still include it.
- If options are detectable (choice items or radio export values), include them in:
  - mapping.json field object as "options": [...]
  - data.json field object as "options": [...] and set value to first option.
- If options are not detectable, omit "options" or set to [] consistently.

D) Implementation details
- Create a new .NET 8 console project in the repo (e.g. /tools/FormToJsonConfigGenerator).
- Add any NuGet packages needed.
- Use PDFsharp if possible. If PDFsharp cannot enumerate AcroForm fields/types/options reliably, then:
  - Keep the solution free/open-source
  - Switch to a free .NET library that can enumerate AcroForm fields + attempt type detection (and options when available).
  - Add code comments documenting why PDFsharp was insufficient and which library is used instead.

Operational requirements
- The tool must:
  1) Scan only the three PDFs listed above in /samples (do not include other PDFs).
  2) Extract fields and attempt type detection and options
  3) Generate mapping.json and data.json into a single output folder (choose /output at repo root)
  4) Print console summary per PDF:
     - PDF name
     - number of fields extracted
     - number typed as text/checkbox/picklist/radio/unknown
     - output file paths

- Add README instructions:
  - how to run: dotnet run
  - where outputs are created
  - how mapping.json jsonPaths point into data.json
  - explain "unknown" type and how a human can correct types/options in mapping.json later

Proceed now:
1) Create the project
2) Implement extraction + type inference + option extraction when possible
3) Run it and fix compile/runtime issues until it works
4) Ensure mapping.json and data.json are generated in /output and included in the workspace

---

## Filling & merging utility

A second .NET console tool is provided under `tools/FormToPdfFiller`
that reads the `output/mapping.json` and `output/data.json` files, fills
and flattens the corresponding PDF forms, and merges all resulting pages
into a single PDF located at `output/merged.pdf`.

Use it after generating the JSON files:

```bash
dotnet run --project tools/FormToPdfFiller
```

The program prints a per‑PDF summary of fields filled and reports the output
path. Flattening is performed by removing the AcroForm dictionary after
populating values, leaving appearance streams on each page.

A Python helper script (`fill_and_merge.py`) is also included for environments
without the .NET SDK; it reads the same JSON files and produces `output/merged.pdf`.

