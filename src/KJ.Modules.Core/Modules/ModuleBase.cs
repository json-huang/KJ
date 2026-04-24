using Prism.Ioc;
using Prism.Modularity;

namespace KJ.Modules.Core.Modules;

/// <summary>
/// 业务模块基类（对齐设计文档 §6.1）。负责统一生命周期与常用依赖注入入口。
/// </summary>
public abstract class ModuleBase : IModule
{
    protected IContainerProvider ContainerProvider { get; private set; } = null!;

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        RegisterServices(containerRegistry);
        RegisterViews(containerRegistry);
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        ContainerProvider = containerProvider;

        RegisterRegions();
        InitializeModule();
    }

    protected abstract void RegisterServices(IContainerRegistry containerRegistry);
    protected abstract void RegisterViews(IContainerRegistry containerRegistry);
    protected abstract void RegisterRegions();
    protected abstract void InitializeModule();
}

