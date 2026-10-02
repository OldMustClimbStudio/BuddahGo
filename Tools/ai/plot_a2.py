"""Plot actual X/Z samples and per-lap diagnostics. Requires Pillow; exports PNG, SVG and JSON."""
import collections, json, math, statistics, sys, html
from pathlib import Path

def render(p):
    samples=[json.loads(x) for x in (p/'trajectory.jsonl').read_text(encoding='utf-8-sig').splitlines() if x]
    events=[json.loads(x) for x in (p/'events.jsonl').read_text(encoding='utf-8-sig').splitlines() if x]
    line=json.loads((p/'racing-line.json').read_text(encoding='utf-8-sig'))['points']
    summary=json.loads((p/'summary.json').read_text(encoding='utf-8-sig'))
    finish=min((e['clock'] for e in events if e['kind']=='authoritative-finish'),default=math.inf)
    driving_contacts=[e for e in events if e['kind']=='collision' and e['clock']<=finish]
    post_finish_contacts=[e for e in events if e['kind']=='collision' and e['clock']>finish]
    comparisons=[json.loads(x['detail']) for x in events if x['kind']=='model-60-ticks']
    free=[x for x in comparisons if not x['Collision']]
    laps=summary.get('laps',[summary.get('lapSeconds')])
    diag={'summary':summary,'event_counts':dict(collections.Counter(x['kind'] for x in events)),
          'sample_ids_contiguous':all(x['sample']==i for i,x in enumerate(samples)),
          'clock_monotonic':all(b['clock']>=a['clock'] for a,b in zip(samples,samples[1:])),
          'gap_ticks':dict(collections.Counter(x['gapTicks'] for x in samples[1:])),
          'lap_range_seconds':max(laps)-min(laps) if laps else None,
          'model_no_contact_windows':len(free),'model_no_contact_pass':sum(x['PositionError']<=.5 and x['VelocityError']<=.5 and x['YawError']<=3 for x in free),
          'model_contact_windows':len(comparisons)-len(free),
          'latest_plan_mean_ms':statistics.mean(x['planMs'] for x in samples),
          'latest_plan_max_ms':max(x['planMs'] for x in samples),'per_lap':[],
          'driving_contacts':len(driving_contacts),'post_finish_contacts':post_finish_contacts}
    # Product LapProgress briefly reports 0 at launch and N+1 after final crossing.
    # Keep those raw labels intact, but group boundary samples into actual race laps.
    def race_lap(x): return min(max(1,x['lap']),summary.get('plannedLaps',1))
    for lap in sorted(set(race_lap(x) for x in samples)):
        rows=[x for x in samples if race_lap(x)==lap]
        following=[x['clock'] for x in samples if race_lap(x)>lap]
        start=rows[0]['clock'] if lap>1 else -math.inf
        end=min(following) if following else math.inf
        contacts=[e for e in driving_contacts if start<=e['clock']<end]
        diag['per_lap'].append(dict(lap=lap,samples=len(rows),mean_abs_lateral=statistics.mean(abs(x['lateral']) for x in rows),contacts=len(contacts)))
    (p/'diagnostics.json').write_text(json.dumps(diag,indent=2),encoding='utf-8')
    positions=line+[x['position'] for x in samples]
    xmin,xmax=min(x['x'] for x in positions),max(x['x'] for x in positions)
    zmin,zmax=min(x['z'] for x in positions),max(x['z'] for x in positions)
    scale=min(1100/(xmax-xmin),820/(zmax-zmin))
    def xy(x):return (50+(x['x']-xmin)*scale,950-(x['z']-zmin)*scale)
    from PIL import Image,ImageDraw,ImageFont
    im=Image.new('RGB',(1200,1030),'white');d=ImageDraw.Draw(im)
    try: font=ImageFont.truetype('Arial.ttf',17)
    except OSError: font=ImageFont.load_default()
    title=f'{p.name}: actual trajectory | laps '+', '.join(f'{x:.3f}s' for x in laps)
    d.text((30,20),title,fill='#172334',font=font)
    d.text((30,48),f'Driving contacts: {len(driving_contacts)} (+{len(post_finish_contacts)} after finish); discontinuities: {summary["discontinuities"]}; errors: {summary["runtimeErrors"]}',fill='#172334',font=font)
    d.text((30,75),'Grey = reference spline (not track boundary); blue/orange/green = laps 1/2/3; red = contact',fill='#172334',font=font)
    d.line([xy(x) for x in line+[line[0]]],fill='#c6cbd2',width=5)
    colours={0:'#555555',1:'#0072b2',2:'#e69f00',3:'#009e73',4:'#009e73'}
    svg=[f'<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1030"><rect width="100%" height="100%" fill="white"/><text x="30" y="30" font-family="sans-serif" font-size="18">{html.escape(title)}</text>']
    def poly(points,col,width):
        if len(points)<2:return
        d.line(points,fill=col,width=width)
        svg.append('<polyline fill="none" stroke="'+col+'" stroke-width="'+str(width)+'" points="'+' '.join(f'{a:.2f},{b:.2f}' for a,b in points)+'"/>')
    poly([xy(x) for x in line+[line[0]]],'#c6cbd2',5)
    gap=statistics.median(x['gapTicks'] for x in samples[1:])
    segment=[];lap=samples[0]['lap']
    for x in samples:
        if x['lap']!=lap or x['discontinuity'] or x['gapTicks']>gap*2:
            poly(segment,colours.get(lap,'#555555'),2);segment=[];lap=x['lap']
        segment.append(xy(x['position']))
    poly(segment,colours.get(lap,'#555555'),2)
    for e in events:
        if e['kind'] not in ['collision','stalled','position-discontinuity']:continue
        if e['clock']>finish:continue
        x,y=xy(e['position']);color='#da3737' if e['kind']=='collision' else '#943dcc'
        d.ellipse((x-3,y-3,x+3,y+3),fill=color)
        svg.append(f'<circle cx="{x:.2f}" cy="{y:.2f}" r="3" fill="{color}"><title>{html.escape(e["kind"])} {e["clock"]:.3f}</title></circle>')
    d.line((30,985,30+100*scale,985),fill='black',width=3)
    d.text((30,997),'100 m | Equal world X/Z scale | Raw source: trajectory.jsonl + events.jsonl',fill='#172334',font=font)
    im.save(p/'trajectory.png');svg.append('</svg>');(p/'trajectory.svg').write_text('\n'.join(svg),encoding='utf-8')
    print(json.dumps(diag,indent=2))

if __name__=='__main__':render(Path(sys.argv[1]))
