import pathlib,json,math,bisect,csv,sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
ROOT=pathlib.Path(sys.argv[1]) if len(sys.argv)>1 else pathlib.Path(__file__).parent
def wrap(a):return (a+180)%360-180
def load(run):
 p=ROOT/'evidence'/run
 rows=[json.loads(s) for s in (p/'trajectory.jsonl').read_text(encoding='utf-8').splitlines()]
 plans=[json.loads(s) for s in (p/'plans.jsonl').read_text(encoding='utf-8').splitlines()]
 events=[json.loads(s) for s in (p/'events.jsonl').read_text(encoding='utf-8').splitlines()]
 finish=next((e['clock'] for e in events if e['kind']=='authoritative-finish'),float('inf'))
 rows=[r for r in rows if r['clock']<=finish]
 times=[r['clock'] for r in plans]; data=[];error=0;lastYaw=None;lastT=None
 for r in rows:
  idx=bisect.bisect_right(times,r['clock'])-1
  if idx<0:continue
  p=plans[idx]['plan'];v=p['tangent'];t=math.degrees(math.atan2(v['x'],v['z']))
  if lastT is None:error=wrap(r['yaw']-t)
  else:error+=wrap(r['yaw']-lastYaw)-wrap(t-lastT)
  lastYaw=r['yaw'];lastT=t
  data.append(dict(elapsed=r['elapsed'],lap=r['lap'],progress=r['progress'],x=r['position']['x'],z=r['position']['z'],
    heading=r['yaw'],visualYaw=r['visualYaw'],cameraYaw=r['cameraYaw'],speed=math.hypot(r['velocity']['x'],r['velocity']['z']),
    yawRate=r['yawRate'],steering=r['steering'],targetX=p['target']['x'],targetZ=p['target']['z'],tangentYaw=t,
    continuousHeadingError=error,segment=p['segment'],planClock=plans[idx]['clock'],planAge=r['clock']-plans[idx]['clock'],
    plannedYawChange=p['selectedYawChange'],neutralCost=p['neutralCost'],leftCost=p['leftCost'],rightCost=p['rightCost'],
    viableFirstKeys=p.get('viableFirstKeys',3),rejectedWinding=p.get('rejectedWinding',0)))
 with (ROOT/'evidence'/f'{run}-aligned.csv').open('w',newline='') as f:
  w=csv.DictWriter(f,fieldnames=data[0].keys());w.writeheader();w.writerows(data)
 models=[json.loads(e['detail']) for e in events if e['kind']=='model-60-ticks']
 clean=[m for m in models if not m['Collision']]
 fail=[m for m in clean if m['PositionError']>.5 or m['VelocityError']>.5 or m['YawError']>3]
 summary=json.loads((ROOT/'evidence'/run/'summary.json').read_text())
 metrics=dict(summary=summary,continuousHeadingRange=[min(r['continuousHeadingError'] for r in data),max(r['continuousHeadingError'] for r in data)],
    samplesOutsideHeadingBranch=sum(abs(r['continuousHeadingError'])>180 for r in data),
    plans=len(plans),plannedYawOver360=sum(abs(r['plan']['selectedYawChange'])>360 for r in plans),
    noViablePlans=sum(r['plan'].get('viableFirstKeys',3)==0 for r in plans),
    drivingContacts=sum(e['kind']=='collision' and e['clock']<=finish for e in events),
    restoredBoxes=sum(e['kind']=='test-box-restored' and 'active=True' in e['detail'] for e in events),
    stallEntries=sum(e['kind']=='stalled' for e in events), respawnEvents=sum(e['kind']=='existing-respawn' for e in events),
    sampleIdsContiguous=all(b['sample']==a['sample']+1 for a,b in zip(rows,rows[1:])),
    clockMonotonic=all(b['clock']>a['clock'] for a,b in zip(rows,rows[1:])),
    sampleGapTicks=sorted(set(r['gapTicks'] for r in rows[1:])),
    modelUnflaggedWindows=len(clean),modelUnflaggedFailures=fail,modelContactWindows=len(models)-len(clean),
    maxPlanAge=max(r['planAge'] for r in data),maxVisualBodyYawDifference=max(abs(wrap(r['heading']-r['visualYaw'])) for r in data))
 return data,metrics
before,bm=load('baseline');after,am=load('fixed')
(ROOT/'evidence/comparison.json').write_text(json.dumps({'baseline':bm,'fixed':am},indent=2),encoding='utf-8')
line=json.loads((ROOT/'evidence/baseline/racing-line.json').read_text())['points']
fig,axes=plt.subplots(2,2,figsize=(14,10),layout='constrained')
for ax,data,title,color in [(axes[0,0],before,'Before: V5, one natural lap','#cf4939'),(axes[0,1],after,'After: winding constraint, three natural laps','#187a98')]:
 ax.plot([r['x'] for r in line],[r['z'] for r in line],color='#bbb',lw=3,label='Track centerline')
 ax.plot([r['x'] for r in data],[r['z'] for r in data],color=color,lw=.8,label='Actual Rigidbody path')
 ax.set_aspect('equal');ax.set_title(title);ax.set_xlabel('World X (m)');ax.set_ylabel('World Z (m)');ax.legend(fontsize=8)
for data,name,color in [(before,'Before','#cf4939'),([r for r in after if r['lap']<=1],'After lap 1','#187a98')]:
 axes[1,0].plot([r['elapsed'] for r in data],[r['continuousHeadingError'] for r in data],label=name,color=color,lw=1)
 section=[r for r in data if .15<=r['progress']<=.30]
 axes[1,1].plot([r['progress']*100 for r in section],[r['continuousHeadingError'] for r in section],label=name,color=color,lw=1.5)
for ax in axes[1]:
 ax.axhline(180,color='#777',ls='--');ax.axhline(-180,color='#777',ls='--');ax.set_ylabel('Continuous body yaw relative to route (deg)');ax.legend();ax.grid(alpha=.2)
axes[1,0].set_xlabel('Natural driving time (s)');axes[1,0].set_title('Extra winding stays visible; angles are not wrapped away')
axes[1,1].set_xlabel('Track progress (%)');axes[1,1].set_title('Same 15–30% track section')
fig.suptitle('AI corner-entry spin diagnosis — observed positions and headings',fontsize=15)
fig.savefig(ROOT/'evidence/spin-comparison.png',dpi=150);fig.savefig(ROOT/'evidence/spin-comparison.svg')
print(json.dumps({'baseline':bm,'fixed':am},indent=2))
