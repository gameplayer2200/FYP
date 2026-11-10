# MCP for Unity 接入步骤（实测通过）

> 从 `AGENTS.md` §6.0 迁出。**一次性环境搭建参考**，日常开发不需要读。
> 环境已在 2026-10-29 配置完成并通过实测；重装机器或换环境时再照此执行。

---

### 6.0 从零接入（2026-10-29 实测通过的完整步骤）

1. **装 MCP server（一次性）**——本机没装 uvx，用 DSH 打包的 uv：
   ```bash
   "D:\DSH\DSH NEXT\resources\app.asar.unpacked\node_modules\@dataiku\uv-win32-x64\bin\uv.exe" tool install mcpforunityserver
   ```
   产物：`C:\Users\14763\.local\bin\mcp-for-unity.exe`（MCP server）与 `unity-mcp.exe`（配套 CLI，走 HTTP 8080，与本通道无关）。uv 会警告该目录不在 PATH——用绝对路径即可，**别为此改系统 PATH**。
2. **让 Unity 侧的 stdio 桥自启**——包里 `EditorConfigurationCache` 的 `UseHttpTransport` 默认取 `true`（HTTP），于是 `StdioBridgeHost.ShouldAutoStartBridge()` 返回 false、6400 端口不会监听。写注册表（EditorPrefs 在 `HKCU\Software\Unity Technologies\Unity Editor 5.x`，**键名后缀 `_h<djb2 变体哈希>`**）：
   - `MCPForUnity.UseHttpTransport_h3850471145` = DWORD `0`（强制 stdio）
   - `MCPForUnity.AutoStartOnLoad_h2539145689` = DWORD `0`（不起 HTTP）
   - `MCPForUnity.ResumeStdioAfterReload_h3690782296` = DWORD `1`（域重载后恢复桥）
   哈希算法：`h=5381; foreach c: h = ((h*33) ^ c) & 0xFFFFFFFF`（不是 java 的 `h*31+c`，也不是 fnv）。
3. **在 DSH profile 注册 server**——`C:\Users\14763\.dsh\profiles\desktop\cordis.patch.yml` 的 `- insert:` 列表里加：
   ```yaml
   - id: mcp-unity
     name: "@deepseek-ai/dsh-mcp-client"
     config:
       serverName: unity
       transport: stdio
       command: 'C:\Users\14763\.local\bin\mcp-for-unity.exe'
       args: ['--transport', 'stdio']
       env:
         HTTP_PROXY: ''
         HTTPS_PROXY: ''
         http_proxy: ''
         https_proxy: ''
         NO_PROXY: 'localhost,127.0.0.1,::1,.local'
         no_proxy: 'localhost,127.0.0.1,::1,.local'
       toolCallTimeoutMs: 300000
       failOnStartupError: false
   ```
   ⚠️ **`env` 段不可省**：本机全局有 `http_proxy=http://127.0.0.1:7897`（Clash 未开），server 内 httpx 解析会抛 `ValueError: invalid literal for int() with base 10: ':1]'`（`normalize_port`）→ stdio server 静默失败（stdout 零输出）。这是"工具列表里没有 unity 工具"最常见的根因。
4. **启动编辑器**：`D:\Unity\Hub\Tuanjie\Hub\Editor\2022.3.62f3\Editor\Unity.exe -projectPath "D:\FYP\FYP"`（Tuanjie/团结 Hub 的安装路径，不是 `C:\Program Files\Unity`）。约 2 分钟后 `127.0.0.1:6400` 进入 LISTENING，`mcp__unity__*` 可用。
5. **验收**：`read_mcp_resource mcpforunity://instances` 应返回单实例 `FYP@<hash>`（port 6400、status running）；`mcpforunity://editor/state` 的 `advice.ready_for_tools=true`；跑一句 `execute_code` 确认。

排障口诀：**端口空 → 查第 2 步的 EditorPrefs；端口有但工具缺失 → 查第 3 步的 env/命令路径；工具有但调用报错 → 查 `Library/MCPForUnity/Logs/`。**

- **主路径（stdio，当前）**：直接调 `mcp__unity__<工具名>`（如 `mcp__unity__execute_code`、`mcp__unity__read_console`）。参数与旧 JSON-RPC 的 `arguments` 完全一致。
- **旧路径（HTTP 传输才可用，留作参考）**：每次调用 = initialize → notifications/initialized → tools/call；响应可能是 SSE `data:` 行或纯 JSON，两者都要兼容：
  ```bash
  # 长代码先落盘成 .cs（工程根 snippets/ 目录），再用 python json.dumps 包成 args.json，杜绝转义问题
  curl -s -X POST http://localhost:8080/mcp -H "Content-Type: application/json" \
    -H "Accept: application/json, text/event-stream" -H "Mcp-Session-Id: <id>" -d @args.json
  ```
  首次握手 initialize 取 `Mcp-Session-Id` 响应头。`snippets/` 已有可复用片段（`pos_check`/`reregister`/`repair_singletons`/`blackdrive`/`drive_lateupdate`/`farlight`）。
- 工具速查：
  - `manage_editor {action:"play"|"stop"}`；进 Play 触发域重载。
  - `execute_code {action:"execute", code:"..."}`：可直呼 `MetroSceneDev`（public static class）的公开静态方法；**`PlayerRef` 在 FYP.Core（asmdef），片段直接写 `typeof(PlayerRef)` 编译不过** → 用反射：`AppDomain` 扫类型名 `"PlayerRef"`，取 `Root`/`Camera` 属性与 `Teleport` 方法（`MetroSceneDev.PlayerRoot()` 是 **private**，同样要反射）。反射小抄：`BindingFlags` 记得带 `Static`；`GetMethod("Update")` 单参匹配会 AmbiguousMatch（用按参数类型精确匹配的助手）；`SelfTest` 签名 `(out string)`；`PortalWindow` 的 `link/srcFrame/portalCam/cooldown` 等均为 private 字段（`GetField` 带 `NonPublic|Public|Instance` 读）。
  - `read_console {action:"get", types:["error","warning"], count:50, format:"plain"}`。
  - `refresh_unity {mode:"force", scope:"scripts", compile:"request", wait_for_ready:true}`（改脚本后；**Play 中改脚本 → 域重载 → 静态注册表全灭**，修法见下）。
  - `manage_camera {action:"screenshot", capture_source:"game_view", ...}` → `Assets/Screenshots/`（**仅 Play 模式有效**；编辑器失焦可能截到旧帧）。
  - 复杂搭建/验证走 `Assets/Editor/MetroSceneDev.cs`（分阶段幂等：`Stage0_Env/Stage0b_Seal/Stage0c_Floors/Stage1_Items/Stage1_Lab/Stage2_Blackout/Stage2_Cabinet/Stage2_Anomalies/Stage3_Carriage/FixPass1/2/Finalize2` + `PT_*` Play 测试驱动 + `Screenshot/FloorMap/ColliderDump` 勘察）。
- **编辑器失焦时 Play 不推进**（`runInBackground=true` 也一样）：计时类逻辑（渐熄、耗电、穿行 LateUpdate）后台测试须反射手动调 `Update`/`LateUpdate`（`snippets/blackdrive.cs`、`drive_lateupdate.cs`）。
- **域重载陷阱**：静态注册表（`PlayerRef`、`BatterySystem.Instance`、`Inventory.Instance`）清空且组件 `Awake` 不重跑 → 跑 `snippets/repair_singletons.cs`；组件内缓存初始化放 `OnEnable`（LightGroup/UnreachableLightAnomaly/PoweredLight 已照此实现）。
- Play 就绪轮询：`MetroSceneDev.PT_Pos()`；Metro 窗要玩家进 15m 预载半径后才就绪（穿行测试范式：`PT_StageAtLabDoor` → 等 1s → `PT_PushThrough` → `drive_lateupdate.cs`）。
- 纪律：MCP 调用短平快、失败就重试；**不要塞永不 resolve 的长 await**；**不要在 Play 运行时在 Assets/ 里增删文件**（原因见 §7.1）。

