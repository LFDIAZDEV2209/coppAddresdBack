namespace CoppAddresd.Auth.Configuration;

public class DevPatientSettings
{
    public const string SectionName = "DevPatient";

    public string DocumentNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
