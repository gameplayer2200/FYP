import subprocess, json, time, sys
TOOL = sys.argv[1] if len(sys.argv) > 1 else "read_console"
ARGS = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {"action":"get","types":["error","warning","log"],"count":40}
proc = subprocess.Popen(["uvx","--from","mcpforunityserver==10.3.0","mcp-for-unity","--transport","stdio"],
    stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, encoding="utf-8")
def send(o): proc.stdin.write(json.dumps(o)+"\n"); proc.stdin.flush()
send({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"z","version":"1"}}})
proc.stdout.readline()
send({"jsonrpc":"2.0","method":"notifications/initialized","params":{}})
send({"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":TOOL,"arguments":ARGS}})
deadline=time.time()+45
while time.time()<deadline:
    line=proc.stdout.readline()
    if not line: break
    try:
        d=json.loads(line)
        if d.get("id")==2:
            r=d.get("result",{})
            c=r.get("content",[{}])[0].get("text","")
            print(c[:4000] if c else json.dumps(r)[:2000])
            break
    except: continue
proc.kill()
