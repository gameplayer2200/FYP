#!/usr/bin/env python3
"""最小 MCP 调用器：initialize → notifications/initialized → tools/call，兼容 SSE / 纯 JSON。

用法:
  python mcp.py <tool> [args.json]
  python mcp.py <tool> -            # args 从 stdin 读 JSON
"""
import json
import sys
import urllib.request

URL = "http://localhost:8080/mcp"


def _post(payload, sid=None):
    headers = {
        "Content-Type": "application/json",
        "Accept": "application/json, text/event-stream",
    }
    if sid:
        headers["Mcp-Session-Id"] = sid
    req = urllib.request.Request(URL, data=json.dumps(payload).encode("utf-8"), headers=headers, method="POST")
    resp = urllib.request.urlopen(req, timeout=300)
    sid_out = resp.headers.get("Mcp-Session-Id")
    body = resp.read().decode("utf-8", "replace")
    out = None
    for line in body.splitlines():
        if line.startswith("data:"):
            out = line[5:].strip()
    if out is None:
        out = body.strip() or None
    return sid_out, out


def call(tool, args):
    sid, _ = _post({
        "jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                   "clientInfo": {"name": "py-mcp", "version": "1"}},
    })
    _post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    _, out = _post({
        "jsonrpc": "2.0", "id": 2, "method": "tools/call",
        "params": {"name": tool, "arguments": args},
    }, sid)
    return out


def main():
    tool = sys.argv[1]
    args = {}
    if len(sys.argv) > 2:
        if sys.argv[2] == "-":
            args = json.load(sys.stdin)
        elif sys.argv[2].lstrip().startswith("{"):
            args = json.loads(sys.argv[2])
        else:
            with open(sys.argv[2], encoding="utf-8") as f:
                args = json.load(f)
    print(call(tool, args))


if __name__ == "__main__":
    main()
