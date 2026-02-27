import PyPDF2, os
root='c:/usr/form-to-pdf'
samples=['form_to_flatten.pdf','Form.pdf','Sample-Fillable-PDF.pdf']
for name in samples:
    path=os.path.join(root,'samples',name)
    try:
        reader=PyPDF2.PdfReader(path)
        fields=reader.get_fields()
        print(name,'=>', list(fields.keys()) if fields else 'no fields')
    except Exception as e:
        print('error',name,e)
