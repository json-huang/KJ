# 流程步骤模块扩展

内置步骤在 `KJ.Workflows/Modules/Builtins`。额外步骤可放到仓库根目录 `modules/workflow/*.dll`，启动时由 `WorkflowStepModuleLoader` 扫描并注册到工具箱。

## 实现接口

```csharp
public sealed class MyStepModule : IWorkflowStepModule
{
    public string Kind => "My.Custom";
    public string Category => "自定义";
    public string DisplayName => "我的步骤";
    public string? Description => "说明文字";
    public int Order => 100;

    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; } =
    [
        new("endpoint", "地址", placeholder: "https://..."),
        new("timeout", "超时(ms)", isReadOnly: false),
    ];

    public void ApplyDefaults(WorkflowStep step)
    {
        step.Parameters.TryAdd("timeout", "5000");
    }
}
```

## 运行时执行（可选）

若步骤需要在流程运行时执行，另实现 `IWorkflowStepHandler` 并在宿主 `App.xaml.cs` 注册（或通过现有 DI 扩展点注册）。

## 属性面板

编辑器根据 `Properties` 动态生成字段，写入 `WorkflowStep.Parameters`。切换步骤类型会刷新字段列表。

## 多连线与运行

- 节点四边有连接点；从任意端口拖到目标端口可建立 `WorkflowDefinition.Links` 中的一条边。
- **普通步骤**：多条出边按顺序依次执行后继（fan-out 队列）。
- **条件分支（Decision）**：仍用 `Branches` + `BranchEvaluator`，每次只走一条匹配分支。
