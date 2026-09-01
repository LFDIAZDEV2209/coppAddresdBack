namespace CoppAddresd.Api;

/// <summary>
/// Marker class para WebApplicationFactory (tests de integración). La clase
/// Program del top-level statements vive en el namespace global y colisiona
/// con la de CoppAddresd.Auth; este tipo único desambigua el entry point.
/// </summary>
public sealed class ApiEntryPoint;