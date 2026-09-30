from pathlib import Path
import json,sys,re,collections
from datetime import datetime
root=Path(sys.argv[1]);out={}
for peer in ('host','client'):
 s={'complete':None,'owners':{},'cases':{},'divergenceMax':dict.fromkeys(['loc','tel','mod','hof'],0),'heartbeats':0,'errors':[]}
 go=None;goUtc=None;case=-1;pending={};lastphase={};lastoffset={};lastRaw={};expired={};resurrections=[]
 for line in (root/(peer+'.jsonl')).open(encoding='utf-8',errors='replace'):
  try:x=json.loads(line);r=x['data'];r['utc']=x['utc']
  except (ValueError,KeyError):continue
  k=r.get('kind');owner=r.get('owner');tick=r.get('localTick',0)
  if owner is not None and owner not in s['owners']:
   s['owners'][owner]={'phases':[],'phaseSamples':collections.Counter(),'corrections':[],'impulses':[],'jumps':[],'resurrections':[]}
  o=s['owners'].get(owner)
  if k=='unlocked':go=tick;goUtc=datetime.fromisoformat(r['utc']);s['go']=r
  if k=='complete':s['complete']=r
  if k=='caseObserved':
   c=r['data'];case=c['index'];s['cases'][case]={'owner':c['owner'],'mode':c['mode'],'projectiles':set(),'hits':set(),'pushes':[],'animations':[],'impulses':[],'collisions':[],'respawns':[]}
  c=s['cases'].get(case)
  if c is not None:
   if k=='matrixSample':
    for p in r['projectiles']:c['projectiles'].add(p['id']);c['hits'].update(p['hits'])
   for event,target in [('pushAccepted','pushes'),('pushAnimation','animations'),('impulse','impulses'),('actorCollision','collisions'),('respawnRequest','respawns')]:
    if k==event:c[target].append(r)
  if k=='run' and r['handoffId']>0 and 'Replayed' not in (r['replicateState'] or ''):
   key=(r.get('obj'),r['handoffId'],r['phase']);previous=lastphase.get(owner)
   if key!=previous:
    o['phases'].append({a:r[a] for a in ['obj','localTick','serverTick','inputTick','phase','handoffId','start','inheritEnd','blendEnd']})
    if previous and previous[:2]==key[:2] and previous[2]=='Normal' and key[2]!='Normal':o['resurrections'].append(r)
    lastphase[owner]=key
   o['phaseSamples'][r['phase']]+=1
  if k=='transformCorrection' and goUtc is not None and 0<=(datetime.fromisoformat(r['utc'])-goUtc).total_seconds()<1:o['corrections'].append(r)
  if k=='reconcile':
   offset=r['serverTick']-tick
   if offset!=lastoffset.get(owner):
    o['jumps'].append({a:r[a] for a in ['localTick','serverTick','snapshotTick','phase','rawPhase','start','rawStart','inheritEnd','rawInheritEnd','blendEnd','rawBlendEnd','modifiers','rawModifiers']}|{'offset':offset,'jump':None if owner not in lastoffset else offset-lastoffset[owner]})
    lastoffset[owner]=offset
   for axis,deadline in enumerate(r['rawModifiers']):
    key=(owner,r.get('obj'),axis,deadline)
    if deadline==0:continue
    active=r['modifiers'][axis]>tick
    if active and expired.get(key):resurrections.append({'owner':owner,'axis':axis,'deadline':deadline,'row':r});expired[key]=False
    if not active:expired[key]=True
  if k=='impulse':o['impulses'].append(r);pending.setdefault(owner,[]).append(r)
  if k in ('postTick','postReplay') and pending.get(owner):
   for impulse in pending.pop(owner):
    impulse['afterPhysics']=r
    actual=r['serverReplayTick'] if k=='postReplay' else r['serverTick']
    impulse['actualSimulationServerTick']=actual;impulse['earlyBy']=max(0,impulse['eventTick']-actual)
  if k=='log':
   msg=r['message']
   if 'HEARTBEAT' in msg:s['heartbeats']+=1
   for axis,n in re.findall(r'(loc|tel|mod|hof)-div=(\d+)',msg):s['divergenceMax'][axis]=max(s['divergenceMax'][axis],int(n))
   if r.get('type') in ('Error','Exception') or 'FATAL' in msg:s['errors'].append(r)
 for c in s['cases'].values():c['projectiles']=sorted(c['projectiles']);c['hits']=sorted(c['hits'])
 s['modifierResurrections']=resurrections
 for o in s['owners'].values():
  corrections=[r['correction'] for r in o['corrections']];o['correctionStats']={'n':len(corrections),'max':max(corrections,default=0),'sum':sum(corrections),'mean':sum(corrections)/len(corrections) if corrections else None}
 out[peer]=s
 print(peer,'complete',s['complete'],'heartbeats',s['heartbeats'],'div',s['divergenceMax'],'errors',len(s['errors']))
 for owner,o in s['owners'].items():
  print(' owner',owner,'phase',o['phases'][:6],'correction',o['correctionStats'],'impulses',len(o['impulses']),'early',sum(i.get('earlyBy',0)>0 for i in o['impulses']),'hofResurrections',len(o['resurrections']))
 print(' modifierResurrections',len(resurrections))
 for i,c in s['cases'].items():print(' case',i,c['mode'],'caster',c['owner'],'projectiles',len(c['projectiles']),'hits',c['hits'],'impulses',len(c['impulses']),'collisions',len(c['collisions']),'respawns',c['respawns'])
(root/'summary.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
