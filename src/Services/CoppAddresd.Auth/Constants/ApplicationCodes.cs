namespace CoppAddresd.Auth.Constants;

/// <summary>
/// Códigos de aplicación conocidos. El código de aplicación es el audience
/// (`aud`) del JWT y la clave de la tabla <c>auth.applications</c>.
/// La autorización de acceso (UserApplication) se resuelve en base de datos,
/// nunca a partir de estos códigos: un rol no está ligado a una aplicación.
/// </summary>
public static class ApplicationCodes
{
    /// <summary>Aplicación móvil para pacientes/usuarios finales.</summary>
    public const string App = "app";

    /// <summary>ERP administrativo.</summary>
    public const string Erp = "erp";

    public static IEnumerable<string> GetAll()
    {
        yield return App;
        yield return Erp;
    }
}