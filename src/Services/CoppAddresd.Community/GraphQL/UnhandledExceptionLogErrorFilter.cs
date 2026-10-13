using HotChocolate;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Community.GraphQL;

/// <summary>
/// Loguea las excepciones no manejadas que HotChocolate envuelve como HC0044
/// "Unexpected Execution Error". Sin este filtro el stack trace nunca llega a
/// CloudWatch y los fallos de producción quedan indistinguibles (incidente I0002:
/// createPostImageUploadInfo con la task def sin taskRoleArn no dejó rastro).
///
/// Se configura DESPUÉS de app.Build() vía <see cref="Configure"/>: el contenedor
/// schema aislado de HotChocolate v16 resuelve las factories de error en su propio
/// contenedor de servicios donde no existe ILoggerFactory (la resolución con DI
/// crashea el arranque con exit 139). El error NO se reescribe: el cliente sigue
/// viendo el mensaje genérico seguro; las GraphQLException intencionales (errores
/// de negocio) no se loguean como error.
/// </summary>
public static class GraphQLUnhandledExceptionLogger
{
    private static ILogger? _logger;

    /// <summary>Delega el logger de la app (llamar una sola vez tras Build).</summary>
    public static void Configure(ILoggerFactory factory) =>
        _logger = factory.CreateLogger("CoppAddresd.Community.GraphQL.UnhandledExceptions");

    /// <summary>Deja el logger en null (tests).</summary>
    public static void Reset() => _logger = null;

    /// <summary>
    /// Filtro de errores de HotChocolate: loguea y devuelve el error intacto.
    /// Nunca lanza, nunca muta: si el logger no está configurado no hace nada.
    /// </summary>
    public static IError Handler(IError error)
    {
        if (error.Exception is not null and not GraphQLException)
        {
            _logger?.LogError(
                error.Exception,
                "GraphQL: excepción no manejada en el resolver. code={ErrorCode} path={ErrorPath} message={ErrorMessage}",
                error.Code,
                error.Path,
                error.Message
            );
        }

        return error;
    }
}
