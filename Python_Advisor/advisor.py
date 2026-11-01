"""Validated packaging recommendation service; deterministic fallback by default.

Optional provider contract: POST the request JSON to PACKFOLD_PROVIDER_URL.
The provider must return a packfold.v1 JSON object directly (no Markdown).
This adapter is not tied to a particular model vendor and is unconfigured by default.
"""
import argparse
import json
import math
import os
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

LIMITS={'width_mm':(60,300),'depth_mm':(50,240),'height_mm':(25,150)}


def validate(spec):
    if not isinstance(spec,dict) or spec.get('schema')!='packfold.v1':
        raise ValueError('Expected schema packfold.v1')
    if spec.get('structure') not in ('open_tray','hinged_lid','lift_off_lid'):
        raise ValueError('Unsupported structure')
    clean={'schema':'packfold.v1','structure':spec['structure']}
    for field,(low,high) in LIMITS.items():
        value=spec.get(field)
        if isinstance(value,bool) or not isinstance(value,(int,float)) or not math.isfinite(value) or not low<=value<=high:
            raise ValueError(f'{field} must be in [{low}, {high}] mm')
        clean[field]=round(value,2)
    clean['source']=str(spec.get('source','unspecified'))[:100]
    clean['rationale']=str(spec.get('rationale',''))[:1200]
    return clean


def recommend(request):
    if not isinstance(request,dict): raise ValueError('Expected request object')
    for key in ('fragile','reusable'):
        if key in request and not isinstance(request[key],bool): raise ValueError(key+' must be boolean')
    dimensions=[]
    for field in ('width','depth','height'):
        value=request.get('product_'+field+'_mm')
        if isinstance(value,bool) or not isinstance(value,(int,float)) or not math.isfinite(value) or value<=0:
            raise ValueError('Invalid product dimension: '+field)
        dimensions.append(value)
    fragile=request.get('fragile',True); reusable=request.get('reusable',True)
    preferred=request.get('preferred_structure')
    if preferred is not None and preferred not in ('open_tray','hinged_lid','lift_off_lid'):
        raise ValueError('Unsupported preferred_structure')
    padding=10 if fragile else 5
    minimums=dict(zip(LIMITS,(dimensions[0]+2*padding,dimensions[1]+2*padding,dimensions[2]+padding+5)))
    for field,value in minimums.items():
        if value>LIMITS[field][1]: raise ValueError('Product plus clearance exceeds supported '+field)
    provider_url=os.environ.get('PACKFOLD_PROVIDER_URL','')
    if provider_url:
        body={'task':'Recommend a paper package for this product. Return only the specified JSON schema.',
              'request':request,'minimum_dimensions_mm':minimums,'allowed_structures':['open_tray','hinged_lid','lift_off_lid'],
              'output_schema':{'schema':'packfold.v1','structure':'hinged_lid','width_mm':180,'depth_mm':120,'height_mm':70,'rationale':'...'}}
        headers={'Content-Type':'application/json'}
        token=os.environ.get('PACKFOLD_PROVIDER_TOKEN')
        if token: headers['Authorization']='Bearer '+token
        req=urllib.request.Request(provider_url,data=json.dumps(body).encode(),headers=headers,method='POST')
        with urllib.request.urlopen(req,timeout=20) as response:
            raw=response.read(65537)
            if len(raw)>65536: raise ValueError('Provider response too large')
            result=validate(json.loads(raw))
        for field,minimum in minimums.items():
            if result[field]<minimum: raise ValueError('Provider design violates product clearance: '+field)
        result['source']='external_ai_provider (user configured)'
        return result
    result={'schema':'packfold.v1','structure':preferred or ('hinged_lid' if reusable or fragile else 'open_tray'),
            **{f:max(LIMITS[f][0],v) for f,v in minimums.items()},'source':'rule_based_fallback (no AI model called)',
            'rationale':f'Added {padding} mm side clearance and {padding+5} mm vertical clearance. '
                        'Kept the requested structure when supplied; otherwise selected a lid for fragile/reusable use. Geometry proposal only; strength is not evaluated.'}
    return validate(result)


class Handler(BaseHTTPRequestHandler):
    def do_POST(self):
        if self.path!='/recommend': self.send_error(404); return
        try:
            length=int(self.headers.get('Content-Length','0'))
            if not 0<length<=16384: raise ValueError('Expected request body up to 16 KB')
            result=recommend(json.loads(self.rfile.read(length)))
            body=json.dumps(result).encode('utf-8'); status=200
        except (ValueError,UnicodeError) as error:
            body=json.dumps({'error':str(error)}).encode(); status=400
        except (OSError,urllib.error.URLError):
            body=b'{"error":"External provider unavailable"}'; status=502
        self.send_response(status); self.send_header('Content-Type','application/json'); self.send_header('Content-Length',str(len(body)))
        self.end_headers(); self.wfile.write(body)

    def log_message(self,format,*args): pass


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--serve',action='store_true'); parser.add_argument('--host',default='127.0.0.1'); parser.add_argument('--port',type=int,default=8765)
    parser.add_argument('--width',type=float,default=160); parser.add_argument('--depth',type=float,default=100); parser.add_argument('--height',type=float,default=55)
    args=parser.parse_args()
    if args.serve:
        print(f'PackFold advisor: http://{args.host}:{args.port}/recommend',flush=True)
        print('Mode: '+('external provider' if os.environ.get('PACKFOLD_PROVIDER_URL') else 'rule-based fallback, NO AI model'),flush=True)
        ThreadingHTTPServer((args.host,args.port),Handler).serve_forever()
    else:
        result=recommend({'product_width_mm':args.width,'product_depth_mm':args.depth,'product_height_mm':args.height,'fragile':True,'reusable':True})
        path=Path(__file__).resolve().parent/'recommendation.json'; path.write_text(json.dumps(result,indent=2),encoding='utf-8')
        print(json.dumps(result,indent=2)); print('Saved:',path)
