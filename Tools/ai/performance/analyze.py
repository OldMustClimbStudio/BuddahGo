from pathlib import Path
import csv,json,statistics,sys
out=Path(__file__).parent
results={}
for name in sys.argv[1:]:
 rows=list(csv.DictReader((out/name/'perf.csv').open()))
 r=[{k:float(v) for k,v in x.items()} for x in rows if 2<=float(x['relative_go'])<=11]
 if not r: continue
 def quant(key):
  a=sorted(x[key] for x in r)
  return {'median':statistics.median(a),'p95':a[int(.95*(len(a)-1))],'max':max(a)}
 duration=sum(x['wall_ms'] for x in r)
 data={'frames':len(r),'wall_seconds':duration/1000,'frame_wall_ms':quant('wall_ms'),'dt_ms':quant('dt_ms'),'net_ticks':sum(x['s12_n'] for x in r),'physics_events':sum(x['s13_n'] for x in r),'fixed_seconds':r[-1]['fixed_time']-r[0]['fixed_time'],'gc_collections':[r[-1]['gc'+str(i)]-r[0]['gc'+str(i)] for i in range(3)],'main_ns':quant('main_ns'),'gc_bytes':quant('gc_bytes'),'gpu_ns':quant('gpu_ns')}
 labels=['ai0','ai1','ai2','ai3','ai4','other_ai','build_inclusive','runinputs','tracker','observer','project','model','net','phys','driver_inclusive','vjitter']
 data['scopes']={label:{'ms':sum(x[f's{i}_ms'] for x in r),'count':sum(x[f's{i}_n'] for x in r),'wall_percent':100*sum(x[f's{i}_ms'] for x in r)/duration} for i,label in enumerate(labels)}
 for v in data['scopes'].values():v['per_call_ms']=v['ms']/v['count'] if v['count'] else None
 results[name]=data
(out/'analysis.json').write_text(json.dumps(results,indent=2));print(json.dumps(results,indent=2))
