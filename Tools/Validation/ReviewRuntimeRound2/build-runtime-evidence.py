from pathlib import Path
import json,hashlib,datetime,collections,sys
root=Path(sys.argv[1])
out=root/'review-r2-evidence';out.mkdir(exist_ok=True)
runs=['baseline-0','baseline-100','fixed-100-matrix','replay-fixed-0','replay-fixed-100','push-contact-0']
report={}
for run in runs:
 if not (root/run/'summary.json').exists():continue
 summary=json.loads((root/run/'summary.json').read_text(encoding='utf-8'))
 cell={}
 for peer in ('host','client'):
  s=summary[peer];selected=[];counts=collections.Counter();seen=set();duplicates=[];allseq=[];last={};offsetRows=[]
  source=root/run/(peer+'.jsonl');hasher=hashlib.sha256()
  for line in source.open('rb'):
   hasher.update(line)
   try:r=json.loads(line);d=r['data']
   except:continue
   k=d.get('kind');counts[k]+=1
   if k in ('unlocked','complete','caseStart','caseObserved','respawnRequest','actorCollision','pushAccepted','pushAnimation','pushSpawn','captureCamera','visualSpawnAnchor','returnedRoom','resultVote'):selected.append(r)
   if k=='impulse':
    selected.append(r);key=(d['owner'],d.get('reconcilePass'),d.get('serverReplayTick'),d['eventTick'],d['logicalId'])
    if key in seen:duplicates.append(r)
    seen.add(key);allseq.append(d.get('consumptionSequence'))
   if k=='reconcile' and d.get('localOwner'):
    offset=d['serverTick']-d['localTick'];delta=offset-last.get(d['owner'],offset);last[d['owner']]=offset
    if delta:offsetRows.append({'utc':r['utc'],'offset':offset,'delta':delta,**d})
  compactOwners={}
  for owner,o in s['owners'].items():
   compactOwners[owner]={'phases':o['phases'],'correctionStats':o['correctionStats'],'resurrections':o['resurrections'],'impulses':[dict(i) for i in o['impulses']]}
  cell[peer]={'sourceSha256':hasher.hexdigest(),'bytes':source.stat().st_size,'complete':s['complete'],'errors':s['errors'],'divergenceMax':s['divergenceMax'],'heartbeats':s['heartbeats'],'owners':compactOwners,'cases':s['cases'],'modifierResurrections':s['modifierResurrections'],'recordCounts':dict(counts),'samePassDuplicateConsumptions':duplicates,'rawConsumptionSequences':allseq,'offsetJumps':offsetRows}
  (out/(run+'-'+peer+'-events.jsonl')).write_text(''.join(json.dumps(x,separators=(',',':'))+'\n' for x in selected),encoding='utf-8')
 report[run]=cell
(out/'runtime-summary.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('Evidence written',out)
