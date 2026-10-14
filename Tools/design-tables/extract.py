"""Helpers to pull effective (serialized) values out of Unity assets for the design tables."""
import os, re, glob, json, math, struct
import yaml
from unity_yaml import load_docs, script_map

ROOT = None
_docs_cache = {}
_asset_guid = None

def set_root(root):
    global ROOT
    ROOT = root

def P(rel):
    return os.path.join(ROOT, rel)

def docs(rel):
    if rel not in _docs_cache:
        _docs_cache[rel] = load_docs(P(rel))
    return _docs_cache[rel]

def asset_map():
    """guid -> asset path (relative) for every .meta under Assets."""
    global _asset_guid
    if _asset_guid is None:
        m = {}
        for meta in glob.glob(os.path.join(ROOT, 'Assets', '**', '*.meta'), recursive=True):
            try:
                with open(meta, encoding='utf-8') as f:
                    for line in f:
                        if line.startswith('guid:'):
                            m[line.split(':', 1)[1].strip()] = os.path.relpath(meta[:-5], ROOT).replace(os.sep, '/')
                            break
            except Exception:
                pass
        _asset_guid = m
    return _asset_guid

def guid_path(ref):
    if not isinstance(ref, dict):
        return ''
    g = ref.get('guid')
    if not g:
        return '' if ref.get('fileID', 0) == 0 else '(scene object)'
    return asset_map().get(g, '?' + g)

def go_names(rel):
    return {d['fileId']: d['data'].get('m_Name') for d in docs(rel) if d['kind'] == 'GameObject'}

def comps(rel, cls=None, go=None):
    """Own MonoBehaviours (script under Assets/Scripts) in a file, optionally filtered by class name / GameObject name."""
    smap = script_map(ROOT)
    names = go_names(rel)
    out = []
    for d in docs(rel):
        if d['kind'] != 'MonoBehaviour':
            continue
        guid = (d['data'].get('m_Script') or {}).get('guid')
        c, spath = smap.get(guid, ('?', ''))
        if cls and c != cls:
            continue
        goid = (d['data'].get('m_GameObject') or {}).get('fileID')
        gname = names.get(goid)
        if go and gname != go:
            continue
        out.append({'class': c, 'go': gname, 'goId': goid, 'fileId': d['fileId'], 'f': d['data']})
    return out

def one(rel, cls, go=None):
    r = comps(rel, cls, go)
    if not r:
        raise KeyError(f'{cls} not found in {rel}')
    return r[0]['f']

def mono_asset(rel):
    """Fields of a single-MonoBehaviour .asset (ScriptableObject)."""
    for d in docs(rel):
        if d['kind'] == 'MonoBehaviour':
            return d['data']
    raise KeyError(rel)

def first_doc(rel, kind):
    for d in docs(rel):
        if d['kind'] == kind:
            return d['data']
    raise KeyError(kind)

def by_fileid(rel, fid):
    for d in docs(rel):
        if d['fileId'] == fid:
            return d
    return None

# ---------- value formatting ----------

def vec(v, n=3):
    if v is None:
        return ''
    keys = ['x', 'y', 'z', 'w'][:n]
    return ','.join(fmt(v.get(k, 0)) for k in keys)

def color(c):
    if c is None:
        return ''
    return ','.join(fmt(c.get(k, 0)) for k in ['r', 'g', 'b', 'a'])

def fmt(x):
    if isinstance(x, bool):
        return 'true' if x else 'false'
    if isinstance(x, float):
        if x == int(x) and abs(x) < 1e9:
            return str(int(x))
        return repr(round(x, 6))
    return str(x)

def curve(c):
    """AnimationCurve -> 'time,value,inSlope,outSlope|...'. Lossless for non-weighted keys."""
    if not c or 'm_Curve' not in c:
        return ''
    keys = []
    for k in c['m_Curve']:
        keys.append(','.join(fmt(k.get(n, 0)) for n in ['time', 'value', 'inSlope', 'outSlope']))
    return '|'.join(keys)

def boolv(x):
    return bool(x) if not isinstance(x, str) else x.lower() in ('1', 'true')

def enum_array_hex(h, size=4):
    """Unity serializes enum arrays as little-endian hex. Return list of ints."""
    if not h:
        return []
    b = bytes.fromhex(h)
    return [struct.unpack('<i', b[i:i + 4])[0] for i in range(0, len(b), 4)]

# ---------- transforms ----------

def _qmul(a, b):
    ax, ay, az, aw = a; bx, by, bz, bw = b
    return (aw*bx + ax*bw + ay*bz - az*by,
            aw*by - ax*bz + ay*bw + az*bx,
            aw*bz + ax*by - ay*bx + az*bw,
            aw*bw - ax*bx - ay*by - az*bz)

def _qrot(q, v):
    qx, qy, qz, qw = q
    p = (v[0], v[1], v[2], 0.0)
    qc = (-qx, -qy, -qz, qw)
    r = _qmul(_qmul(q, p), qc)
    return (r[0], r[1], r[2])

def world_transform(rel, go_fileid):
    """World position/rotation(euler y)/scale of a GameObject by walking m_Father."""
    tf_by_go = {}
    tf_by_id = {}
    for d in docs(rel):
        if d['kind'] in ('Transform', 'RectTransform'):
            tf_by_go[(d['data'].get('m_GameObject') or {}).get('fileID')] = d
            tf_by_id[d['fileId']] = d
    t = tf_by_go.get(go_fileid)
    chain = []
    while t:
        chain.append(t['data'])
        f = (t['data'].get('m_Father') or {}).get('fileID', 0)
        t = tf_by_id.get(f) if f else None
    pos = (0.0, 0.0, 0.0); rot = (0.0, 0.0, 0.0, 1.0); scale = (1.0, 1.0, 1.0)
    for td in reversed(chain):
        lp = td.get('m_LocalPosition', {}); lr = td.get('m_LocalRotation', {}); ls = td.get('m_LocalScale', {})
        lpv = (lp.get('x', 0), lp.get('y', 0), lp.get('z', 0))
        scaled = (lpv[0]*scale[0], lpv[1]*scale[1], lpv[2]*scale[2])
        rotated = _qrot(rot, scaled)
        pos = (pos[0]+rotated[0], pos[1]+rotated[1], pos[2]+rotated[2])
        lq = (lr.get('x', 0), lr.get('y', 0), lr.get('z', 0), lr.get('w', 1))
        rot = _qmul(rot, lq)
        scale = (scale[0]*ls.get('x', 1), scale[1]*ls.get('y', 1), scale[2]*ls.get('z', 1))
    # yaw (Y euler) from quaternion
    qx, qy, qz, qw = rot
    siny = 2*(qw*qy + qz*qx)
    cosy = 1 - 2*(qx*qx + qy*qy)
    yaw = math.degrees(math.atan2(siny, cosy))
    return pos, yaw, scale

# ---------- misc ----------

def json_file(rel):
    with open(P(rel), encoding='utf-8') as f:
        return json.load(f)

def playable_duration(rel):
    """Max clip end over all TimelineClips (m_Start + m_Duration)."""
    end = 0.0
    with open(P(rel), encoding='utf-8') as f:
        text = f.read()
    for m in re.finditer(r'm_Start: ([\d.eE+-]+)\s*\n\s*m_ClipIn: [\d.eE+-]+\s*\n(?:.*\n){0,6}?\s*m_Duration: ([\d.eE+-]+)', text):
        end = max(end, float(m.group(1)) + float(m.group(2)))
    for m in re.finditer(r'm_StopTime: ([\d.eE+-]+)', text):
        end = max(end, float(m.group(1)))
    return round(end, 4)

def timeline_signals(rel):
    out = []
    with open(P(rel), encoding='utf-8') as f:
        text = f.read()
    for m in re.finditer(r'm_Time: ([\d.eE+-]+)\s*\n(?:\s*m_\w+: [^\n{]*\n){0,4}?\s*m_Asset: \{fileID: \d+, guid: ([0-9a-f]+)', text):
        out.append((float(m.group(1)), asset_map().get(m.group(2), m.group(2))))
    return out

def tmp_texts(rel):
    """All TextMeshPro texts in a scene/prefab: (gameObject name, text)."""
    names = go_names(rel)
    out = []
    for d in docs(rel):
        if d['kind'] == 'MonoBehaviour' and 'm_text' in d['data']:
            goid = (d['data'].get('m_GameObject') or {}).get('fileID')
            out.append((names.get(goid), d['data'].get('m_text')))
    return out

def dropdown_options(rel):
    names = go_names(rel)
    out = {}
    for d in docs(rel):
        if d['kind'] == 'MonoBehaviour' and 'm_Options' in d['data'] and isinstance(d['data']['m_Options'], dict):
            goid = (d['data'].get('m_GameObject') or {}).get('fileID')
            opts = [o.get('m_Text') for o in d['data']['m_Options'].get('m_Options', [])]
            out[names.get(goid)] = {'options': opts, 'value': d['data'].get('m_Value')}
    return out

def cs_string_literals(rel, exclude_patterns=(r'Log\(', r'Warning\(', r'Error\(', r'Exception\(', r'nameof\(', r'Assert', r'Verbose', r'GUILayout', r'MenuItem', r'Tooltip', r'\[Header', r'throw ', r'Info\(', r'Debug\.', r'NetDebug', r'GameLog', r'NetLog', r'PlayerPrefs', r'const string')):
    """(line, literal) for user-facing string literals in a C# file, excluding log/diagnostic lines."""
    out = []
    ex = [re.compile(p) for p in exclude_patterns]
    with open(P(rel), encoding='utf-8') as f:
        for i, line in enumerate(f, 1):
            s = line.strip()
            if s.startswith('//') or any(p.search(line) for p in ex):
                continue
            for m in re.finditer(r'(\$?)"((?:[^"\\]|\\.)*)"', line):
                lit = m.group(2)
                if len(lit) < 2:
                    continue
                if not re.search(r'[A-Za-z一-鿿]', lit):
                    continue
                if re.fullmatch(r'[A-Za-z0-9_.]+', lit) and not re.search(r'[一-鿿]', lit) and ' ' not in lit:
                    continue  # identifiers, keys, scene names
                if lit.startswith('[') or '{(' in lit or re.fullmatch(r'\{[^}]*\}', lit) or lit.startswith('<'):
                    continue  # log tags, pure format expressions
                out.append((i, lit))
    return out


def resolve_ref_name(rel, fid):
    """Name for a fileID inside rel; follows stripped docs into their source prefab."""
    d = by_fileid(rel, fid)
    if not d:
        return str(fid)
    names = go_names(rel)
    if d['stripped'] or d['data'].get('m_PrefabInstance', {}).get('fileID'):
        pi = by_fileid(rel, (d['data'].get('m_PrefabInstance') or {}).get('fileID', 0))
        src_fid = (d['data'].get('m_CorrespondingSourceObject') or {}).get('fileID', 0)
        if pi:
            prefab = guid_path(pi['data'].get('m_SourcePrefab') or {})
            if prefab and not prefab.startswith('?'):
                sd = by_fileid(prefab, src_fid)
                if sd:
                    gid = (sd['data'].get('m_GameObject') or {}).get('fileID') if sd['kind'] != 'GameObject' else sd['fileId']
                    nm = go_names(prefab).get(gid, '')
                    return f"{os.path.basename(prefab)}:{nm}" if nm else os.path.basename(prefab)
                return os.path.basename(prefab)
    gid = (d['data'].get('m_GameObject') or {}).get('fileID') if d['kind'] != 'GameObject' else d['fileId']
    return names.get(gid, str(fid))

def sprite_of_image(rel, image_fid):
    d = by_fileid(rel, image_fid)
    if not d:
        return ''
    if 'm_Sprite' in d['data']:
        return guid_path(d['data']['m_Sprite'])
    gid = (d['data'].get('m_GameObject') or {}).get('fileID') if d['kind'] != 'GameObject' else d['fileId']
    for o in docs(rel):
        if o['kind'] == 'MonoBehaviour' and 'm_Sprite' in o['data'] and (o['data'].get('m_GameObject') or {}).get('fileID') == gid:
            return guid_path(o['data']['m_Sprite'])
    return ''
