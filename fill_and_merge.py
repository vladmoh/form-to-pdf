import json, os
from PyPDF2 import PdfReader, PdfWriter

root = 'c:/usr/form-to-pdf'
with open(os.path.join(root,'output','mapping.json')) as f:
    mapping = json.load(f)
with open(os.path.join(root,'output','data.json')) as f:
    data = json.load(f)

writer = PdfWriter()

for pdfName, cfg in mapping.items():
    src = os.path.join(root, cfg['source'])
    reader = PdfReader(src)
    # compute values dict
    values = {}
    df = data['documents'][pdfName]['fields']
    for fname, fcfg in cfg['fields'].items():
        values[fname] = df[fname]['value']
    # apply to each page
    for page in reader.pages:
        writer.update_page_form_field_values(page, values)
    # add pages to output
    writer.append_pages_from_reader(reader)

outpath = os.path.join(root,'output','merged.pdf')
with open(outpath,'wb') as f:
    writer.write(f)
print('merged written', outpath)
