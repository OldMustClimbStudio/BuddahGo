"""Create Unity .meta files for new files/folders under Assets that lack one (so a commit never ships meta-less assets).
Usage: python Tools/design-tables/add_metas.py <repo_root> [subdir ...]
"""
import os, sys, uuid

root = sys.argv[1]
targets = sys.argv[2:] or ['Assets/Design', 'Assets/Scripts/Config', 'Assets/Scripts/Editor/DesignTables', 'Assets/Tests/EditMode']
FOLDER = 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
DEFAULT = 'DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
TEXT = 'TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
MONO = 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'

def write(path, body):
    mp = path + '.meta'
    if os.path.exists(mp):
        return False
    with open(mp, 'w', encoding='utf-8', newline='\n') as f:
        f.write('fileFormatVersion: 2\nguid: %s\n%s' % (uuid.uuid4().hex, body))
    return True

created = []
for target in targets:
    base = os.path.join(root, target)
    if not os.path.isdir(base):
        continue
    if write(base, FOLDER):
        created.append(base)
    for d, dirs, files in os.walk(base):
        for sub in dirs:
            p = os.path.join(d, sub)
            if write(p, FOLDER):
                created.append(p)
        for f in files:
            if f.endswith('.meta'):
                continue
            p = os.path.join(d, f)
            ext = os.path.splitext(f)[1].lower()
            body = MONO if ext == '.cs' else TEXT if ext in ('.csv', '.json', '.md', '.txt') else DEFAULT
            if write(p, body):
                created.append(p)
for c in created:
    print('meta', os.path.relpath(c, root))
print(len(created), 'created')
