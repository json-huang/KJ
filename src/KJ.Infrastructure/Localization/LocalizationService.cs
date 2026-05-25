using System.Globalization;
using System.Resources;

namespace KJ.Infrastructure.Localization;

/// <summary>
/// 多语言服务。支持运行时切换语言。
/// </summary>
public sealed class LocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _resources = new();
    private string _currentCulture = "zh-CN";

    public event Action? LanguageChanged;

    public string CurrentCulture => _currentCulture;

    public LocalizationService()
    {
        // 注册内置语言包
        RegisterChineseSimplified();
        RegisterEnglish();
    }

    /// <summary>获取本地化字符串。</summary>
    public string GetString(string key)
    {
        if (_resources.TryGetValue(_currentCulture, out var lang) && lang.TryGetValue(key, out var value))
            return value;

        // 回退到中文
        if (_resources.TryGetValue("zh-CN", out var fallback) && fallback.TryGetValue(key, out var fallbackValue))
            return fallbackValue;

        return key; // 返回 key 本身作为兜底
    }

    /// <summary>获取本地化字符串（带格式化参数）。</summary>
    public string GetString(string key, params object[] args)
    {
        var template = GetString(key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    /// <summary>切换语言。</summary>
    public void SetCulture(string culture)
    {
        if (_currentCulture == culture) return;
        _currentCulture = culture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        LanguageChanged?.Invoke();
    }

    /// <summary>获取可用语言列表。</summary>
    public IReadOnlyList<string> GetAvailableCultures() => _resources.Keys.ToList().AsReadOnly();

    /// <summary>注册自定义语言包。</summary>
    public void Register(string culture, Dictionary<string, string> strings)
    {
        _resources[culture] = strings;
    }

    private void RegisterChineseSimplified()
    {
        _resources["zh-CN"] = new Dictionary<string, string>
        {
            // 通用
            ["App.Title"] = "KJ 工业自动化平台",
            ["Common.Save"] = "保存",
            ["Common.Cancel"] = "取消",
            ["Common.Delete"] = "删除",
            ["Common.Refresh"] = "刷新",
            ["Common.Search"] = "搜索",
            ["Common.Loading"] = "加载中...",
            ["Common.Error"] = "错误",
            ["Common.Success"] = "成功",
            ["Common.Confirm"] = "确认",

            // 导航
            ["Nav.Dashboard"] = "仪表盘",
            ["Nav.Monitoring"] = "监控",
            ["Nav.Devices"] = "设备",
            ["Nav.Tags"] = "标签",
            ["Nav.Alarms"] = "告警",
            ["Nav.Config"] = "配置",
            ["Nav.Reporting"] = "报表",
            ["Nav.Workflows"] = "工作流",
            ["Nav.Auth"] = "权限管理",

            // 登录
            ["Login.Title"] = "登录",
            ["Login.Email"] = "邮箱",
            ["Login.Password"] = "密码",
            ["Login.RememberEmail"] = "记住邮箱",
            ["Login.StaySignedIn"] = "保持登录",
            ["Login.SignIn"] = "登录",
            ["Login.Error"] = "登录失败",

            // 仪表盘
            ["Dashboard.Devices"] = "设备",
            ["Dashboard.Connected"] = "在线",
            ["Dashboard.Alarms"] = "活动报警",
            ["Dashboard.SystemStatus"] = "系统状态",
            ["Dashboard.RecentEvents"] = "最近事件",

            // 设备
            ["Device.List"] = "设备列表",
            ["Device.Name"] = "名称",
            ["Device.Type"] = "类型",
            ["Device.State"] = "状态",
            ["Device.Host"] = "地址",
            ["Device.Port"] = "端口",
            ["Device.Add"] = "添加设备",
            ["Device.Remove"] = "移除设备",

            // 告警
            ["Alarm.Active"] = "活动报警",
            ["Alarm.Acknowledge"] = "确认",
            ["Alarm.Clear"] = "清除",
            ["Alarm.Severity"] = "严重程度",
            ["Alarm.TriggeredAt"] = "触发时间",

            // 工作流
            ["Workflow.List"] = "工作流列表",
            ["Workflow.Editor"] = "流程编辑",
            ["Workflow.Run"] = "运行",
            ["Workflow.Step"] = "单步",
            ["Workflow.Pause"] = "暂停",
            ["Workflow.Resume"] = "继续",
            ["Workflow.Cancel"] = "取消",
            ["Workflow.Save"] = "保存",

            // 报表
            ["Report.Export"] = "导出",
            ["Report.Query"] = "查询",
            ["Report.From"] = "开始时间",
            ["Report.To"] = "结束时间",
        };
    }

    private void RegisterEnglish()
    {
        _resources["en"] = new Dictionary<string, string>
        {
            ["App.Title"] = "KJ Industrial Automation Platform",
            ["Common.Save"] = "Save",
            ["Common.Cancel"] = "Cancel",
            ["Common.Delete"] = "Delete",
            ["Common.Refresh"] = "Refresh",
            ["Common.Search"] = "Search",
            ["Common.Loading"] = "Loading...",
            ["Common.Error"] = "Error",
            ["Common.Success"] = "Success",
            ["Common.Confirm"] = "Confirm",

            ["Nav.Dashboard"] = "Dashboard",
            ["Nav.Monitoring"] = "Monitoring",
            ["Nav.Devices"] = "Devices",
            ["Nav.Tags"] = "Tags",
            ["Nav.Alarms"] = "Alarms",
            ["Nav.Config"] = "Config",
            ["Nav.Reporting"] = "Reporting",
            ["Nav.Workflows"] = "Workflows",
            ["Nav.Auth"] = "Auth",

            ["Login.Title"] = "Sign In",
            ["Login.Email"] = "Email",
            ["Login.Password"] = "Password",
            ["Login.RememberEmail"] = "Remember email",
            ["Login.StaySignedIn"] = "Stay signed in",
            ["Login.SignIn"] = "Sign In",
            ["Login.Error"] = "Sign in failed",

            ["Dashboard.Devices"] = "Devices",
            ["Dashboard.Connected"] = "Online",
            ["Dashboard.Alarms"] = "Active Alarms",
            ["Dashboard.SystemStatus"] = "System Status",
            ["Dashboard.RecentEvents"] = "Recent Events",

            ["Device.List"] = "Device List",
            ["Device.Name"] = "Name",
            ["Device.Type"] = "Type",
            ["Device.State"] = "State",
            ["Device.Host"] = "Host",
            ["Device.Port"] = "Port",
            ["Device.Add"] = "Add Device",
            ["Device.Remove"] = "Remove Device",

            ["Alarm.Active"] = "Active Alarms",
            ["Alarm.Acknowledge"] = "Acknowledge",
            ["Alarm.Clear"] = "Clear",
            ["Alarm.Severity"] = "Severity",
            ["Alarm.TriggeredAt"] = "Triggered At",

            ["Workflow.List"] = "Workflow List",
            ["Workflow.Editor"] = "Workflow Editor",
            ["Workflow.Run"] = "Run",
            ["Workflow.Step"] = "Step",
            ["Workflow.Pause"] = "Pause",
            ["Workflow.Resume"] = "Resume",
            ["Workflow.Cancel"] = "Cancel",
            ["Workflow.Save"] = "Save",

            ["Report.Export"] = "Export",
            ["Report.Query"] = "Query",
            ["Report.From"] = "From",
            ["Report.To"] = "To",
        };
    }
}
