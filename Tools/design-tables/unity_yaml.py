"""Minimal Unity YAML reader: splits multi-document files, resolves MonoBehaviour script GUIDs to class names."""
import re, os, glob, json, sys
import yaml

HEADER = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?\s*$')

def load_docs(path):
    docs = []
    with open(path, encoding='utf-8') as f:
        text = f.read()
    parts = re.split(r'^(--- !u!\d+ &-?\d+(?: stripped)?)\s*$', text, flags=re.M)
    for i in range(1, len(parts), 2):
        m = HEADER.match(parts[i])
        body = re.sub(r'^(\s+\w+: )([0-9a-fA-F]{8,})\s*$', lambda m: m.group(1) + '"' + m.group(2) + '"', parts[i + 1], flags=re.M)
        try:
            data = yaml.safe_load(body)
        except Exception as e:
            data = {'__parse_error': str(e)}
        if not isinstance(data, dict) or not data:
            continue
        kind = next(iter(data))
        docs.append({'classId': int(m.group(1)), 'fileId': int(m.group(2)), 'stripped': bool(m.group(3)), 'kind': kind, 'data': data[kind]})
    return docs

_guid_cache = None

def script_map(root):
    global _guid_cache
    if _guid_cache is not None:
        return _guid_cache
    m = {}
    for meta in glob.glob(os.path.join(root, 'Assets', '**', '*.cs.meta'), recursive=True):
        try:
            with open(meta, encoding='utf-8') as f:
                for line in f:
                    if line.startswith('guid:'):
                        guid = line.split(':', 1)[1].strip()
                        cs = meta[:-5]
                        rel = os.path.relpath(cs, root).replace(os.sep, '/')
                        m[guid] = (os.path.splitext(os.path.basename(cs))[0], rel)
                        break
        except Exception:
            pass
    _guid_cache = m
    return m

def components(root, path, only_own=True):
    smap = script_map(root)
    docs = load_docs(path)
    go_names = {d['fileId']: d['data'].get('m_Name') for d in docs if d['kind'] == 'GameObject'}
    for d in docs:
        if d['kind'] != 'MonoBehaviour':
            continue
        s = d['data'].get('m_Script') or {}
        guid = s.get('guid')
        cls, spath = smap.get(guid, ('?' + str(guid), ''))
        if only_own and not spath.startswith('Assets/Scripts/'):
            continue
        go = (d['data'].get('m_GameObject') or {}).get('fileID')
        fields = {k: v for k, v in d['data'].items() if not k.startswith('m_')}
        yield {'class': cls, 'script': spath, 'gameObject': go_names.get(go), 'fileId': d['fileId'], 'fields': fields, 'name': d['data'].get('m_Name')}

def all_docs(root, path):
    return load_docs(path)

if __name__ == '__main__':
    root, path = sys.argv[1], sys.argv[2]
    out = list(components(root, path, only_own=('--all' not in sys.argv)))
    json.dump(out, sys.stdout, ensure_ascii=False, indent=1, default=str)
