namespace KJ.Workflows.Modules;

/// <summary>
/// 流程编辑器工具箱模块契约：实现此接口的类型可通过模块目录扫描自动出现在工具箱。
/// 运行时执行仍由 <see cref="IWorkflowStepHandler"/> 负责（可同程序集实现）。
/// </summary>
public interface IWorkflowStepModule
{
    /// <summary>步骤类型标识，与 <see cref="WorkflowStep.Kind"/> 及 Handler.CanHandle 一致。</summary>
    string Kind { get; }

    string Category { get; }

    string DisplayName { get; }

    string Description { get; }

    /// <summary>工具箱排序，越小越靠前。</summary>
    int Order { get; }

    /// <summary>该模块在属性面板中显示的字段（不含通用的标题/连线）。</summary>
    IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; }

    /// <summary>新建步骤时写入默认 <see cref="WorkflowStep.Parameters"/>。</summary>
    void ApplyDefaults(WorkflowStep step);
}
