# KJ — 工业自动化监控平台

基于 .NET 8 的模块化工业 SCADA/MES 系统，WinUI 3 前端 + Prism 模块化架构。

## 项目结构

```
KJ/
├── src/
│   ├── KJ.App/                          # WinUI 3 主应用（Shell、导航、登录）
│   ├── KJ.Domain/                       # 领域模型（Tag、Device、Alarm、Recipe、Audit）
│   ├── KJ.Core/                         # 核心运行时（TagStore、CommsService）
│   ├── KJ.Workflows/                    # 工作流引擎（定义 + 运行时）
│   ├── KJ.Drivers/                      # 设备驱动（Modbus TCP/RTU、TCP、OPC UA）
│   ├── KJ.Drivers.Abstractions/         # 驱动抽象接口
│   ├── KJ.Drivers.Plc.Beckhoff.Ads/     # Beckhoff ADS PLC 驱动
│   ├── KJ.Comms.Abstractions/           # 通信层抽象（Transport、Protocol）
│   ├── KJ.Comms.Drivers.Tcp/            # TCP 通信实现
│   ├── KJ.Diagnostics/                  # 诊断追踪（DiagnosticHub）
│   ├── KJ.Infrastructure/               # 基础设施（EF Core、MassTransit、Identity、Auth）
│   ├── KJ.Infrastructure.Migrations.SqlServer/  # SQL Server 迁移
│   ├── KJ.Modules.Core/                 # 模块基类、Region 定义
│   ├── KJ.Modules.Auth/                 # 认证授权模块（用户/角色管理）
│   ├── KJ.Modules.Alarm/                # 告警模块
│   ├── KJ.Modules.Config/               # 配置模块
│   ├── KJ.Modules.Monitoring/           # 监控模块（WPF 版）
│   ├── KJ.Modules.Monitoring.WinUI/     # 监控模块（WinUI 3 版，含工作流编辑器）
│   └── KJ.Modules.Reporting/            # 报表模块
├── tests/
│   ├── KJ.Domain.Tests/                 # 领域层单元测试
│   └── KJ.Infrastructure.Tests/         # 基础设施层单元测试
├── tools/
│   └── SeedHistory/                     # 历史数据种子工具
└── docs/                                # 设计文档
```

## 技术栈

| 层 | 技术 |
|---|---|
| 前端 | WinUI 3 + Prism (Uno) |
| 领域 | 纯 C# records/interfaces |
| 驱动 | NModbus4 (Modbus TCP/RTU), OPC UA (规划中), Beckhoff ADS |
| 数据库 | EF Core + SQL Server |
| 消息总线 | MassTransit |
| 认证 | ASP.NET Core Identity + JWT |
| 弹性策略 | Polly 8 (Retry + Circuit Breaker) |
| 诊断 | 自定义 DiagnosticHub (OpenTelemetry 风格) |

## 支持的设备驱动

- **Modbus TCP** — NModbus4 TCP/IP，支持 Coil/DI/Holding/Input 寄存器
- **Modbus RTU** — NModbus4 串口 RTU，支持 COM 端口配置
- **TCP 原始协议** — 原始 TCP Socket 通信
- **Beckhoff ADS** — Beckhoff PLC 通信
- **OPC UA** — 规划中（TODO）

## 模块化架构

基于 Prism 的模块系统，每个业务模块包含：
- **Module** — 模块入口（继承 `ModuleBase`）
- **Views** — WinUI 页面
- **ViewModels** — MVVM 视图模型
- **Services** — 模块内部服务

模块通过 Region 系统注入到 Shell 的导航和内容区域。

## 工作流引擎

支持可视化工业流程编排：
- 画布拖拽式步骤编排
- 步骤间连线（NextStepId）
- 运行时状态机（Idle → Running → Paused → Completed/Failed/Canceled）
- 单步调试模式
- 运行日志记录

## 快速开始

### 前置条件
- .NET 8 SDK
- Visual Studio 2022 17.10+ 或 Rider
- SQL Server（可选，InMemory 用于开发）

### 构建
```bash
dotnet build KJ.slnx
```

### 运行测试
```bash
dotnet test
```

## 架构原则

1. **领域驱动** — Domain 层无外部依赖，纯 POCO
2. **依赖倒置** — 通过接口隔离驱动/通信/持久化
3. **模块解耦** — 业务模块通过 Prism Region 和消息总线通信
4. **弹性通信** — 驱动层内置 Retry + Circuit Breaker
5. **诊断可观测** — 全链路 DiagnosticEvent 追踪
