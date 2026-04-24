namespace KJ.Domain.Security;

/// <summary>
/// 权限常量（与框架设计文档 5.2 节对齐，供 UI 与领域逻辑引用）。
/// </summary>
public static class Permissions
{
    public const string DeviceView = "device:view";
    public const string DeviceConfigure = "device:configure";
    public const string DeviceControl = "device:control";

    public const string TagView = "tag:view";
    public const string TagWrite = "tag:write";
    public const string TagConfigure = "tag:configure";

    public const string AlarmView = "alarm:view";
    public const string AlarmAcknowledge = "alarm:acknowledge";
    public const string AlarmConfigure = "alarm:configure";

    public const string RecipeView = "recipe:view";
    public const string RecipeEdit = "recipe:edit";
    public const string RecipeApply = "recipe:apply";

    public const string UserView = "user:view";
    public const string UserManage = "user:manage";
    public const string RoleManage = "role:manage";

    public const string SystemConfigure = "system:configure";
    public const string AuditView = "audit:view";
}
