#!/usr/bin/env python3
"""探测本机 Unity MCP 监听端口：对候选端口发 initialize，通的那个就是。"""
import json
import urllib.request

PAYLOAD = {
    "jsonrpc": "2.0", "id": 1, "method": "initialize",
    "params": {"protocolVersion": "2024-11-05", "capabilities": {},
               "clientInfo": {"name": "probe", "version": "1"}},
}

CANDIDATES = [8080, 6401, 14939, 56052, 56200, 56624, 34999, 38000, 38443, 55504]

for port in CANDIDATES:
    for host in ("127.0.0.1", "localhost"):
        req = urllib.request.Request(
            "http://%s:%d/mcp" % (host, port),
            data=json.dumps(PAYLOAD).encode(),
            headers={"Content-Type": "application/json",
                     "Accept": "application/json, text/event-stream"},
            method="POST")
        try:
            resp = urllib.request.urlopen(req, timeout=4)
            body = resp.read().decode("utf-8", "replace")[:300]
            print("PORT %d (%s) -> %d | sid=%s | %s" % (port, host, resp.status,
                  resp.headers.get("Mcp-Session-Id"), body.replace("\n", " ")))
            break
        except Exception as e:
            print("PORT %d (%s) -> %s" % (port, host, type(e).__name__))
