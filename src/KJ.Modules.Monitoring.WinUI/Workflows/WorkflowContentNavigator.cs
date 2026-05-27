using KJ.Modules.Core.Diagnostics;

using KJ.Modules.Core.Regions;

using KJ.Modules.Monitoring.ViewModels;

using KJ.Modules.Monitoring.Views;

using KJ.Workflows;

using Prism.Ioc;

using Prism.Navigation;

using Prism.Navigation.Regions;



namespace KJ.Modules.Monitoring.Workflows;



/// <summary>

/// 绕过 Prism RequestNavigate 对 WorkflowEditor 失效的问题，直接 Add + Activate 区域视图。

/// </summary>

public interface IWorkflowContentNavigator

{

    void ShowEditor(INavigationParameters parameters);

}



public sealed class WorkflowContentNavigator : IWorkflowContentNavigator

{

    private readonly IRegionManager _regionManager;

    private readonly IContainerProvider _container;



    public WorkflowContentNavigator(IRegionManager regionManager, IContainerProvider container)

    {

        _regionManager = regionManager;

        _container = container;

    }



    public void ShowEditor(INavigationParameters parameters)

    {

        NavTrace.Write("WorkflowContentNavigator.ShowEditor: start");



        if (!_regionManager.Regions.ContainsRegionWithName(RegionNames.MainContent))

        {

            NavTrace.Write("WorkflowContentNavigator.ShowEditor: MainContent region missing");

            return;

        }



        var region = _regionManager.Regions[RegionNames.MainContent];

        // Prefer reusing an existing center page instance if present.
        if (WorkflowCenterPage.TryOpenFromAnywhere(parameters))
        {
            NavTrace.Write("WorkflowContentNavigator.ShowEditor: forwarded to active WorkflowCenterPage");
            return;
        }

        WorkflowNavigationBridge.SetPending(parameters);

        try
        {
            foreach (var view in region.Views.ToArray())
                region.Remove(view);

            var center = _container.Resolve<WorkflowCenterPage>();
            region.Add(center);
            region.Activate(center);

            var activeName = region.ActiveViews.FirstOrDefault()?.GetType().Name ?? "null";
            NavTrace.Write($"WorkflowContentNavigator.ShowEditor: activeView={activeName}");
        }

        catch (Exception ex)

        {

            NavTrace.Write($"WorkflowContentNavigator.ShowEditor: FAILED {ex}");

            throw;

        }

    }

}

