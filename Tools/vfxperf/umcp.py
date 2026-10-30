"""Minimal streamable-HTTP MCP client for the local Unity MCP server.
Usage: python -X utf8 umcp.py <tool> '<json args>'   or   python umcp.py --code file.cs
Keeps the session id in umcp_session.txt so repeated calls reuse one session."""
import json, sys, os, urllib.request, io

URL = "http://127.0.0.1:8080/mcp"
HERE = os.path.dirname(os.path.abspath(__file__))
SESS = os.path.join(HERE, "umcp_session.txt")
INSTANCE = os.environ.get("UMCP_INSTANCE", "vfx-refactor@20ffbfa46a852d00")
_id = [0]

def post(payload, session=None):
    _id[0] += 1
    payload = dict(payload); payload.setdefault("jsonrpc", "2.0")
    if "method" in payload and not payload["method"].startswith("notifications/"):
        payload["id"] = _id[0]
    data = json.dumps(payload).encode()
    req = urllib.request.Request(URL, data=data, method="POST")
    req.add_header("Content-Type", "application/json")
    req.add_header("Accept", "application/json, text/event-stream")
    if session: req.add_header("Mcp-Session-Id", session)
    try:
        resp = urllib.request.urlopen(req, timeout=600)
    except urllib.error.HTTPError as e:
        return None, e.read().decode(errors="replace"), e.headers
    body = resp.read().decode(errors="replace")
    sid = resp.headers.get("Mcp-Session-Id")
    # SSE or JSON
    result = None
    if "text/event-stream" in (resp.headers.get("Content-Type") or ""):
        for line in body.splitlines():
            if line.startswith("data:"):
                try: result = json.loads(line[5:].strip())
                except Exception: pass
    else:
        try: result = json.loads(body) if body.strip() else None
        except Exception: result = None
    return result, body, sid

def new_session():
    r, body, sid = post({"method": "initialize", "params": {"protocolVersion": "2025-03-26", "capabilities": {}, "clientInfo": {"name": "umcp", "version": "0.1"}}})
    if not sid: raise SystemExit("no session id: " + body[:300])
    post({"method": "notifications/initialized"}, sid)
    open(SESS, "w").write(sid)
    r, body, _ = post({"method": "tools/call", "params": {"name": "set_active_instance", "arguments": {"instance": INSTANCE}}}, sid)
    return sid

def session():
    if os.path.exists(SESS): return open(SESS).read().strip()
    return new_session()

def call(tool, args, retry=True):
    sid = session()
    r, body, _ = post({"method": "tools/call", "params": {"name": tool, "arguments": args}}, sid)
    if r is None or ("error" in r and retry):
        if retry and (r is None or "session" in json.dumps(r).lower() or "No Unity" in body):
            sid = new_session()
            r, body, _ = post({"method": "tools/call", "params": {"name": tool, "arguments": args}}, sid)
    return r, body

def text_of(r):
    try:
        parts = r["result"]["content"]
        return "\n".join(p.get("text", "") for p in parts)
    except Exception:
        return json.dumps(r, ensure_ascii=False)[:4000]

if __name__ == "__main__":
    if sys.argv[1] == "--code":
        code = io.open(sys.argv[2], encoding="utf-8").read()
        r, body = call("execute_code", {"action": "execute", "code": code, "compiler": "auto", "safety_checks": len(sys.argv) < 4})
    else:
        args = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
        r, body = call(sys.argv[1], args)
    out = text_of(r) if r else body
    print(out[:12000])
