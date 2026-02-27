import PyPDF2, os, json

root='c:/usr/form-to-pdf'
samples=['form_to_flatten.pdf','Form.pdf','Sample-Fillable-PDF.pdf']

mapping = {}
data = {"documents":{}}

for name in samples:
    path=os.path.join(root,'samples',name)
    try:
        reader=PyPDF2.PdfReader(path)
        fields=reader.get_fields() or {}
        thismap={'source':f'samples/{name}','fields':{}}
        thisdata={'fields':{}}
        for fname,finfo in fields.items():
            # infer
            fieldType='unknown'
            opts=None
            # access raw dictionary
            raw = finfo.get('/FT')
            if raw:
                raw = str(raw)
                if '/Btn' in raw:
                    # button: look for /Yes/Off maybe; assume checkbox
                    fieldType='checkbox'
                elif '/Tx' in raw:
                    fieldType='text'
                elif '/Ch' in raw:
                    fieldType='picklist'
            # PyPDF2 may not deliver /FT; fallback heuristics by name
            if fieldType=='unknown':
                if 'Check' in fname or 'Option' in fname and 'Option ' in fname:
                    fieldType='checkbox'
                elif 'Dropdown' in fname or 'List' in fname:
                    fieldType='picklist'
                elif fname.lower() in ['gender','married']:
                    fieldType='radio'
                else:
                    fieldType='text'
            # try options
            if '/Opt' in finfo:
                opts = [str(o) for o in finfo['/Opt']]
            if '/Kids' in finfo:
                # maybe radio group inside kids
                pass
            jsonPath=f"$.documents['{name}'].fields['{fname}'].value"
            fieldmap={'type':fieldType,'jsonPath':jsonPath}
            if opts:
                fieldmap['options']=opts
            thismap['fields'][fname]=fieldmap
            fv=None
            if fieldType=='text' or fieldType=='unknown': fv=f"TEST_{fname}"
            elif fieldType=='checkbox': fv=True
            elif fieldType=='picklist': fv=opts[0] if opts else 'OPTION_1'
            elif fieldType=='radio': fv=opts[0] if opts else 'CHOICE_1'
            fielddata={'type':fieldType,'value':fv}
            if opts: fielddata['options']=opts
            thisdata['fields'][fname]=fielddata
        mapping[name]=thismap
        data['documents'][name]=thisdata
    except Exception as e:
        print('error',name,e)

os.makedirs(os.path.join(root,'output'),exist_ok=True)
with open(os.path.join(root,'output','mapping.json'),'w') as f:
    json.dump(mapping,f,indent=2)
with open(os.path.join(root,'output','data.json'),'w') as f:
    json.dump(data,f,indent=2)
print('done')
