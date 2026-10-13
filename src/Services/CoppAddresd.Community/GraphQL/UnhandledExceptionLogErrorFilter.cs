using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Community.GraphQL;

/// <summary>
/// Loguea las excepciones no manejadas que HotChocolate envuelve como HC0044
/// "Unexpected Execution Error". Sin este filtro el stack trace nunca llega a
/// CloudWatch y los fallos de producción quedan indistinguibles (incidente I0002:
/// createPostImageUploadInfo con la task def sin taskRoleArn no dejó rastro).
/// El error NO se reescribe: el cliente sigue viendo el mensaje genérico seguro;
/// las GraphQLException intencionales (errores de negocio) no se loguean como error.
/// </summary>
internal sealed class UnhandledExceptionLogErrorFilter(
    ILogger<UnhandledExceptionLogErrorFilter> logger
) : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is not null and not GraphQLException)
        {
            logger.LogError(
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
