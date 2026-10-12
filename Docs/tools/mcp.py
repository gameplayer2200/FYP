import subprocess, json, time, sys
# 用法: py mcp.py '<C#代码>' —— 经 v10.3 stdio 桥执行 C#
CODE = sys.argv[1] if len(sys.argv) > 1 else 'return "no code";'
proc = subprocess.Popen(["uvx","--from","mcpforunityserver==10.3.0","mcp-for-unity","--transport","stdio"],
    stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, encoding="utf-8")
def send(o): proc.stdin.write(json.dumps(o)+"\n"); proc.stdin.flush()
send({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"z","version":"1"}}})
proc.stdout.readline()
send({"jsonrpc":"2.0","method":"notifications/initialized","params":{}})
send({"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"execute_code","arguments":{"action":"execute","code":CODE,"safety_checks":False}}})
deadline=time.time()+45
while time.time()<deadline:
    line=proc.stdout.readline()
    if not line: break
    try:
        d=json.loads(line)
        if d.get("id")==2:
            r=d.get("result",{})
            c=r.get("content",[{}])[0].get("text","")
            print(c[:1000] if c else json.dumps(r)[:400])
            break
    except: continue
proc.kill()
