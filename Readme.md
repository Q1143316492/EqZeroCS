# EqZeroCS

C# 移植版的 wlengine（参考 `target/wlengine-asyncio` 的 Python 版本）。
初版只保留单进程 TCP 服务器 + 模板控制台客户端，便于后续在 Unity 中复用 `Shared`。

## 布局

```
EqZeroCS/
├── EqZeroCS.sln
├── config/
│   ├── server_config.json    # 与 Python 版同名同结构
│   └── client_config.json
└── src/
    ├── Shared/               # netstandard2.1，准备软链给 Unity
    │   ├── Net/Message.cs    # [4B BE len][4B BE flag][msgpack body]
    │   ├── Net/MessageFlag.cs
    │   ├── Logging/Log.cs
    │   └── Config/ConfigLoader.cs
    ├── Server/               # net8.0 控制台进程，单端口 TCP
    │   ├── Net/TcpServer.cs
    │   ├── Net/ServerConnection.cs
    │   ├── App/ServerApp.cs
    │   └── Program.cs        # --name login (默认)
    └── Client/               # net8.0 控制台模板客户端
        ├── Net/ClientConnection.cs
        ├── App/ClientApp.cs
        └── Program.cs
```

## 协议

与 Python 版完全一致，便于跨端联调：

```
┌──────────────┬──────────────┬───────────────────────┐
│ length (4B)  │  flag (4B)   │   msgpack body (N)    │
│ BE int32     │  BE int32    │                       │
└──────────────┴──────────────┴───────────────────────┘
```

- `length`：body 字节数
- `flag`：消息类型，`0 = Base`，`1 = RRpcCall`
- `body`：msgpack 序列化的 `Dictionary<string, object?>`

## 运行

```powershell
# 编译
dotnet build EqZeroCS.sln

# 启动 login 服务器（默认 --name login）
dotnet run --project src/Server

# 另开终端，启动模板客户端
dotnet run --project src/Client
```

客户端会连接 `config/client_config.json` 中的 login 地址，发送一条
`{op:"login", user:"cwl", pwd:"123"}` 消息，服务器收到后会回显并打上
`server` / `echo` 字段，双方控制台都能看到日志。

## 给 Unity 用

`src/Shared` 目标框架为 `netstandard2.1`，不依赖 `System.Net.Sockets` 之外
的服务端 API，可以直接软链到 Unity 项目的 `Assets/Plugins/EqZero/Shared`：

```powershell
# 在 Unity 项目下
New-Item -ItemType SymbolicLink -Path Assets\Plugins\EqZero\Shared `
  -Target D:\Workspace\AI\EqZeroCS\src\Shared
```

之后 Unity 端只需自带的 `MessagePack-CSharp` 包即可使用同一套 `Message` /
`MessageFlag`，复用客户端的连接代码可放在另一个独立的 Unity 友好 csproj 中。

## 后续路线（对齐 Python 版）

- [ ] 链式 RPC 调用封装（`rpc_wrapper.py`）
- [ ] 多进程服务（gam / gate / gas / ats / gcc）+ 启动脚本生成
- [ ] Tick 系统（单次 / 循环）
- [ ] 配置驱动的服务注册
