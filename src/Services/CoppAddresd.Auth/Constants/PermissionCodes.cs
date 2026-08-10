namespace CoppAddresd.Auth.Constants;

public static class PermissionCodes
{
    public const string UsersView = "Users.View";
    public const string UsersCreate = "Users.Create";
    public const string UsersUpdate = "Users.Update";
    public const string UsersDelete = "Users.Delete";

    public const string RolesView = "Roles.View";
    public const string RolesCreate = "Roles.Create";
    public const string RolesUpdate = "Roles.Update";
    public const string RolesDelete = "Roles.Delete";
    public const string RolesAssign = "Roles.Assign";

    public const string PermissionsView = "Permissions.View";
    public const string PermissionsAssign = "Permissions.Assign";

    public const string AgentsView = "Agents.View";
    public const string AgentsCreate = "Agents.Create";
    public const string AgentsUpdate = "Agents.Update";
    public const string AgentsDelete = "Agents.Delete";

    public static IEnumerable<string> GetAll()
    {
        yield return UsersView;
        yield return UsersCreate;
        yield return UsersUpdate;
        yield return UsersDelete;
        yield return RolesView;
        yield return RolesCreate;
        yield return RolesUpdate;
        yield return RolesDelete;
        yield return RolesAssign;
        yield return PermissionsView;
        yield return PermissionsAssign;
        yield return AgentsView;
        yield return AgentsCreate;
        yield return AgentsUpdate;
        yield return AgentsDelete;
    }

    public static string GetModule(string permissionCode)
    {
        return permissionCode.Split('.')[0];
    }

    public static string GetAction(string permissionCode)
    {
        return permissionCode.Split('.')[1];
    }
}
