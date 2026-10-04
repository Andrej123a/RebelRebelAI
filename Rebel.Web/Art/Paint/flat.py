# Splits each Art/Looks drawing into flat layers for the painter: colour (no lighting
# filters), ink (the drawn lines and inked shapes), fx, and a z-order map (ids), as SVG
# files in ./svg/{key}-{layer}.svg under the current directory.
import os, re, copy, sys, xml.etree.ElementTree as ET
DIR=os.environ.get('PAINT_DRAWINGS') or os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','Looks')
NS='http://www.w3.org/2000/svg'
ET.register_namespace('', NS)
q=lambda t:'{%s}%s'%(NS,t)
os.makedirs('svg',exist_ok=True)
keys=sys.argv[1:] or [f[:-4] for f in sorted(os.listdir(DIR)) if f.endswith('.svg')]
for key in keys:
    raw=open(f'{DIR}/{key}.svg',encoding='utf-8').read()
    body=raw.split('\n',1)[1]
    root=ET.fromstring(body)
    for el in root.iter():
        f=el.get('filter','')
        if f in ('url(#rs-volume)','url(#rs-cast)','url(#rs-pencil)'): el.attrib.pop('filter')
        if el.get('mask')=='url(#rs-silhouette)': el.attrib.pop('mask')
    def layer(keep):
        r=copy.deepcopy(root)
        for parent in r.iter():
            for ch in list(parent):
                c=ch.get('class','')
                for k in ('rs-look-print','rs-look-ink','rs-look-fx'):
                    if k in c.split() and k!=keep: parent.remove(ch)
        return ET.tostring(r,encoding='unicode')
    for name,keep in [('colour','rs-look-print'),('ink','rs-look-ink'),('fx','rs-look-fx')]:
        open(f'svg/{key}-{name}.svg','w').write(layer(keep))
    print(key)

# The z-order map: every solid shape of the print layer gets its own id colour (red
# channel = index), so the painter knows what lies on top of what and can cast shadows.
DRAW={'path','circle','ellipse','rect','polygon','polyline','line'}
def eff(el,parents,attr):
    v=el.get(attr)
    if v is not None: return v
    for p in parents:
        if p.get(attr) is not None: return p.get(attr)
    return 'black' if attr=='fill' else 'none'
for key in keys:
    raw=open(f'{DIR}/{key}.svg',encoding='utf-8').read()
    root=ET.fromstring(raw.split('\n',1)[1])
    for parent in root.iter():
        for ch in list(parent):
            c=ch.get('class','').split()
            if 'rs-look-ink' in c or 'rs-look-fx' in c: parent.remove(ch)
    parents={c:p for p in root.iter() for c in p}
    def chain(el):
        out=[]
        while el in parents:
            el=parents[el]; out.append(el)
        return out
    prt=next(g for g in root.iter(q('g')) if 'rs-look-print' in g.get('class',''))
    n=0
    for el in prt.iter():
        tag=el.tag.split('}')[1]
        if tag not in DRAW: continue
        anc=chain(el)
        fill=eff(el,anc,'fill'); stroke=eff(el,anc,'stroke')
        op=1.0
        for e in [el]+anc:
            for a in ('opacity','fill-opacity'):
                if e.get(a): op*=float(e.get(a))
        soft=any(e.get('filter','').startswith('url(#rs-soft') for e in [el]+anc)
        physical = op>0.95 and not soft
        n+=1
        idc='#%02x%02x00'%(n & 255, n >> 8)
        el.set('fill', idc if physical and fill!='none' and not fill.startswith('url') else 'none')
        el.set('stroke', idc if physical and stroke!='none' and not stroke.startswith('url') else 'none')
    for e in root.iter():
        for a in ('opacity','fill-opacity','stroke-opacity','filter','mask'):
            e.attrib.pop(a,None)
    root.set('shape-rendering','crispEdges')
    open(f'svg/{key}-ids.svg','w').write(ET.tostring(root,encoding='unicode'))
    print(key,'ids',n)
