from pathlib import Path
import hashlib, json, math, sys

root, destination = map(Path, sys.argv[1:3])
destination.mkdir(parents=True, exist_ok=True)
visual = root / 'visual-warm-100'
result = {'run': visual.name, 'peers': {}, 'limits': ['Local same-build dual Editor, not cross-machine.', 'Screenshot sampling is not continuous 60 fps video.', 'Case 13 slows after its first shot; do not describe its entire capture as 80 u/s.', 'Observer Rigidbody velocity is not rendered remote speed.', 'Visual acceptance belongs to the user.']}
for role in ('host', 'client'):
    log = visual / (role + '.jsonl')
    rows = [json.loads(line) for line in log.read_text(encoding='utf-8').splitlines()]
    selected = []
    for row in rows:
        data = row['data']
        if data.get('kind') in ('start', 'complete', 'captureCamera', 'videoFrame', 'visualSpawnAnchor'):
            if 'project' in data: del data['project']
            if 'file' in data: data['file'] = '/'.join(Path(data['file']).parts[-2:])
            selected.append(row)
    peer = {'rawLogSha256': hashlib.sha256(log.read_bytes()).hexdigest(),
            'materialReadiness': json.loads((visual/(role+'-material-readiness.json')).read_text(encoding='utf-8')),
            'events': selected, 'cases': {}}
    for case in (12, 13):
        frames = [r['data'] for r in selected if r['data']['kind']=='videoFrame' and r['data']['index']==case]
        speed = [math.sqrt(sum(v*v for v in f['velocity'])) for f in frames]
        peer['cases'][case] = {'frames':len(frames), 'compilingFrames':sum(f['shaderCompiling'] for f in frames),
            'recordedRigidbodySpeedRange':[min(speed),max(speed)], 'rttRangeMs':[min(f['rtt'] for f in frames),max(f['rtt'] for f in frames)],
            'latencyEnabled':all(f['latencyEnabled'] for f in frames), 'simulatedLatencyMs': sorted(set(f['simulatedLatency'] for f in frames))}
    result['peers'][role] = peer
(destination/'visual-warm-summary.json').write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')

# Keep evidence around the observed +1 offset update, including forward simulation.
# Include both complete rematch/return-room paths; never call historical active snapshots a live resurrection.
cycle = root / 'visual-corrected-100'
selected = []
for role in ('host','client'):
    for line in (cycle/(role+'.jsonl')).open(encoding='utf-8'):
        row = json.loads(line); data=row['data']; kind=data.get('kind')
        keep = kind in ('unlocked','endMatchButton','resultVote','returnedRoom','complete')
        if role=='client' and data.get('localOwner') and data.get('localTick') in (8049,8050,8051):
            keep |= kind=='reconcile' or kind=='run' and 'Replayed' not in data.get('replicateState','')
        if keep:selected.append({'peer':role,**row})
(destination/'cycle-r9-selected.jsonl').write_text('\n'.join(json.dumps(x,separators=(',',':')) for x in selected)+'\n',encoding='utf-8')
print('Wrote',len(selected),'cycle/R9 events and visual evidence to',destination)
