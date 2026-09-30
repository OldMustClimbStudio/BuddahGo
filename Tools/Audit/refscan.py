"""Dead-code check for Assets/Scripts.

For each script, report how many scene/prefab/asset YAML files reference its
.meta GUID and how many other C# files mention a type it declares. A script is
a deletion candidate only when both counts are 0 AND it is not started through
RuntimeInitializeOnLoadMethod, AddComponent, editor menus or reflection.

Usage (repo root):  python3 Tools/Audit/refscan.py [Type ...]
Without arguments prints every script with 0 YAML refs and 0 C# refs.
LFS pointer files are skipped, so run after `git lfs pull` for full coverage.
"""
import os
import re
import sys

ROOT = "Assets"
YAML_EXT = (".unity", ".prefab", ".asset", ".controller", ".playable", ".overrideController")


def load_yaml():
    out = []
    for dp, _, fn in os.walk(ROOT):
        for f in fn:
            if f.endswith(YAML_EXT):
                p = os.path.join(dp, f)
                with open(p, "rb") as fh:
                    b = fh.read()
                if b.startswith(b"version https://git-lfs"):
                    continue
                out.append((p, b.decode("utf-8", "ignore")))
    return out


def load_cs():
    out = {}
    for dp, _, fn in os.walk(ROOT):
        for f in fn:
            if f.endswith(".cs"):
                p = os.path.join(dp, f)
                with open(p, encoding="utf-8", errors="ignore") as fh:
                    out[p] = re.sub(r"//.*", "", fh.read())
    return out


def main():
    wanted = set(sys.argv[1:])
    yaml = load_yaml()
    cs = load_cs()
    print("file\tyamlRefs\tcsRefs\tstartupHooks")
    for p, src in sorted(cs.items()):
        if not p.replace(os.sep, "/").startswith("Assets/Scripts/"):
            continue
        types = re.findall(r"(?:class|struct|interface|enum)\s+@?([A-Za-z_]\w*)", src)
        if wanted and not wanted.intersection(types):
            continue
        guid = None
        if os.path.exists(p + ".meta"):
            m = re.search(r"guid: ([0-9a-f]+)", open(p + ".meta").read())
            guid = m.group(1) if m else None
        yref = sum(1 for _, t in yaml if guid and guid in t)
        cref = 0
        for q, s in cs.items():
            if q != p and any(re.search(r"\b" + re.escape(t) + r"\b", s) for t in types):
                cref += 1
        hooks = "RuntimeInitializeOnLoadMethod" in src or "MenuItem" in src or "InitializeOnLoad" in src
        if wanted or (yref == 0 and cref == 0):
            print(f"{p}\t{yref}\t{cref}\t{hooks}")


if __name__ == "__main__":
    main()
