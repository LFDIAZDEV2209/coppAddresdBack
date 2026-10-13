using CoppAddresd.Community.GraphQL;
using HotChocolate;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CoppAddresd.Community.UnitTests;

/// <summary>
/// Regresión del filtro de excepciones no manejadas (incidente I0002): el
/// handler debe devolver el error INTACTO y nunca lanzar, con o sin logger
/// configurado. Las GraphQLException de negocio no se tratan como error.
/// </summary>
public class GraphQLUnhandledExceptionLoggerTests
{
    private static IError ErrorConExcepcion(Exception ex) =>
        ErrorBuilder.New().SetMessage("mensaje del error").SetException(ex).Build();

    [Fact]
    public void Sin_logger_configurado_no_lanza_y_devuelve_el_mismo_error()
    {
        GraphQLUnhandledExceptionLogger.Reset();
        var error = ErrorConExcepcion(new InvalidOperationException("boom"));

        var resultado = GraphQLUnhandledExceptionLogger.Handler(error);

        Assert.Same(error, resultado);
    }

    [Fact]
    public void Con_logger_configurado_loguea_y_devuelve_el_mismo_error()
    {
        GraphQLUnhandledExceptionLogger.Configure(NullLoggerFactory.Instance);
        var error = ErrorConExcepcion(new InvalidOperationException("boom"));

        var resultado = GraphQLUnhandledExceptionLogger.Handler(error);

        Assert.Same(error, resultado);
    }

    [Fact]
    public void GraphQLException_de_negocio_no_se_loguea_como_error()
    {
        GraphQLUnhandledExceptionLogger.Configure(NullLoggerFactory.Instance);
        var error = ErrorConExcepcion(new GraphQLException("No se encontró la encuesta."));

        var resultado = GraphQLUnhandledExceptionLogger.Handler(error);

        Assert.Same(error, resultado);
    }
}
