#!/usr/bin/env python3
"""stdio 版最小 MCP 探针：起 mcp-for-unity stdio 子进程，走 JSON 行协议。

用法:
  python mcp_stdio.py                          # 仅握手 + tools/list
  python mcp_stdio.py <tool> '<json args>'     # 额外做一次 tools/call
"""
import json
import queue
import subprocess
import sys
import tempfile
import threading
import time

CMD = ["uvx", "--from", "mcpforunityserver==10.3.0", "mcp-for-unity", "--transport", "stdio"]


class Client:
    def __init__(self, errfile):
        self.errfile = errfile
        self.proc = subprocess.Popen(
            CMD, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=errfile,
            text=True, encoding="utf-8", bufsize=1)
        self.q = queue.Queue()
        self._id = 0

        def reader():
            for line in self.proc.stdout:
                self.q.put(line)

        threading.Thread(target=reader, daemon=True).start()

    def send(self, obj):
        self.proc.stdin.write(json.dumps(obj) + "\n")
        self.proc.stdin.flush()

    def request(self, method, params=None, timeout=60):
        self._id += 1
        rid = self._id
        msg = {"jsonrpc": "2.0", "id": rid, "method": method}
        if params is not None:
            msg["params"] = params
        self.send(msg)
        deadline = time.time() + timeout
        while time.time() < deadline:
            try:
                line = self.q.get(timeout=max(0.1, deadline - time.time()))
            except queue.Empty:
                break
            try:
                m = json.loads(line)
            except Exception:
                continue  # 桥的非 JSON 日志行，跳过
            if m.get("id") == rid:
                return m
        return None


def main():
    errpath = tempfile.mktemp(prefix="mcp_stdio_err_")
    with open(errpath, "w", encoding="utf-8") as errfile:
        c = Client(errfile)

        init = c.request("initialize", {
            "protocolVersion": "2024-11-05", "capabilities": {},
            "clientInfo": {"name": "zcode-stdio-probe", "version": "1"},
        }, timeout=90)
        if init is None:
            print("FAIL: initialize timeout")
            _dump_stderr(errpath)
            return 1
        if init.get("error"):
            print("FAIL: initialize error:", json.dumps(init["error"], ensure_ascii=False))
            return 1
        info = init.get("result", {}).get("serverInfo", {})
        print("OK initialize:", info.get("name"), info.get("version"))

        c.send({"jsonrpc": "2.0", "method": "notifications/initialized"})

        tools_resp = c.request("tools/list", {}, timeout=60)
        if tools_resp is None:
            print("FAIL: tools/list timeout")
            _dump_stderr(errpath)
            return 1
        tools = tools_resp.get("result", {}).get("tools", [])
        print("TOOLS:", len(tools))
        print(" ".join(t["name"] for t in tools))

        if len(sys.argv) > 1:
            tool = sys.argv[1]
            args = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
            call = c.request("tools/call", {"name": tool, "arguments": args}, timeout=180)
            if call is None:
                print("FAIL: tools/call timeout")
                _dump_stderr(errpath)
                return 1
            print("CALL:", json.dumps(call.get("result", call.get("error")), ensure_ascii=False)[:3000])

    try:
        c.proc.terminate()
    except Exception:
        pass
    return 0


def _dump_stderr(errpath):
    try:
        with open(errpath, encoding="utf-8", errors="replace") as f:
            tail = f.read()[-1500:]
        print("--- bridge stderr tail ---")
        print(tail)
    except OSError:
        pass


if __name__ == "__main__":
    sys.exit(main())
