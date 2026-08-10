namespace CoppAddresd.Auth.Configuration;

public class AuthSettings
{
    public const string SectionName = "Auth";

    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminFirstName { get; set; } = "Admin";
    public string AdminLastName { get; set; } = "System";
}
