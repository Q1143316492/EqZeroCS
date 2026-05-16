# EqZeroCS

C# 移植版的 wlengine（参考 `temp_code/wlengine-asyncio` 的 Python 版本），对齐多进程 TCP 服务架构与链式 RPC 协议。

## 进程拓扑

```
Client ──→ login                (port 4000)
Client ──→ gate1 / gate2        (port 5000 / 5001)
               │
               ├──→ gcc1        (port 5300)
               ├──→ gas1        (port 5100)
               └──→ ats1        (port 5200)
```

- **login**：登录入口，验证客户端身份
- **gate**：前门代理，维持客户端长连接，按 RRpcGet 前缀将 RPC 帧路由到对应后端
- **gcc**：游戏逻辑（GccPlayer / GccPlayerMgr）
- **gas**：通用异步服务
- **ats**：异步任务服务

进程配置见 `config/server_config.json`。

## 布局

```
EqZeroCS/
├── EqZeroCS.sln
├── config/
│   ├── server_config.json    # 所有进程的 ip/port
│   └── client_config.json    # 客户端连接的 login 地址
└── src/
    ├── Shared/               # netstandard2.1，可软链给 Unity
    │   ├── Net/
    │   │   ├── Message.cs        # [4B BE len][4B BE flag][msgpack body]
    │   │   ├── MessageFlag.cs    # Base=0, RRpcCall=1
    │   │   ├── TcpAcceptor.cs    # 监听端，接受入站连接
    │   │   ├── TcpConnection.cs  # 单条 TCP 连接读写
    │   │   └── TcpDialer.cs      # 出站连接 + 重试
    │   ├── Rpc/
    │   │   ├── RpcProtocol.cs    # wire key 常量，与 Python const.py 对齐
    │   │   ├── RpcMessage.cs     # 构造 / 解析 RPC 帧
    │   │   ├── RpcDispatcher.cs  # 按 call-chain 反射分发
    │   │   ├── RpcProxy.cs       # 客户端发起 RPC 调用
    │   │   ├── RpcCallContext.cs # 当前请求上下文（连接 + rpcDefine）
    │   │   └── IRpcRoute.cs      # RRpcGet* 路由接口
    │   ├── Logging/Log.cs
    │   └── Config/ConfigLoader.cs
    ├── Server/               # net8.0，单可执行多进程类型
    │   ├── Program.cs            # --name <login|gate1|gas1|gcc1|ats1>
    │   ├── Framework/
    │   │   ├── ServerAppBase.cs  # 持有 TcpAcceptor，子类实现 OnMessage
    │   │   └── ServerRegistry.cs # 读取 server_config.json，解析进程类型前缀
    │   ├── Login/LoginApp.cs
    │   ├── Gate/GateApp.cs
    │   ├── Gas/GasApp.cs
    │   ├── Gcc/GccApp.cs + GccPlayer.cs + GccPlayerMgr.cs
    │   └── Ats/AtsApp.cs
    └── Client/               # net8.0 控制台模板客户端
        ├── Program.cs
        ├── ClientApp.cs
        └── ClientObj.cs
```

## 协议

与 Python 版完全一致，便于跨端联调：

```
┌──────────────┬──────────────┬───────────────────────┐
│ length (4B)  │  flag (4B)   │   msgpack body (N)    │
│ BE int32     │  BE int32    │                       │
└──────────────┴──────────────┴───────────────────────┘
```

- `length`：body 字节数（不含头部）
- `flag`：`0 = Base`，`1 = RRpcCall`
- `body`：msgpack 序列化的 `Dictionary<string, object?>`

RPC 帧 body 固定两个 key（与 `framwork/const.py ERpcProtocol` 对齐）：

| key | 含义 |
|-----|------|
| `"1"` | rpcDefine：4 元组 `(fromProcess, fromGlobalId, toProcess, toGlobalId)` |
| `"2"` | callChain：`[[funcName, args[]], ...]` |

## 运行

```bat
REM 编译 + 启动全部服务进程（6 个窗口）
scripts\server.bat

REM 另开终端，启动模板客户端
scripts\client.bat
```

`server.bat` 启动顺序：`gcc1 → ats1 → gas1`（等 2 s）`→ gate1 / gate2`（等 1 s）`→ login`。  
各进程在出站连接失败时会自动重试，因此顺序不是强依赖。

也可以单独启动某个进程：

```bat
dotnet run --project src/Server -- --name gate1
```

## 给 Unity 用

`src/Shared` 目标框架为 `netstandard2.1`，可直接软链到 Unity 项目：

```powershell
# 在 Unity 项目根目录下执行
New-Item -ItemType SymbolicLink -Path Assets\Plugins\EqZero\Shared `
  -Target D:\Workspace\AI\EqZeroCS\src\Shared
```

Unity 端引入 `MessagePack-CSharp` 包后即可复用 `Message` / `RpcProxy` 等。
