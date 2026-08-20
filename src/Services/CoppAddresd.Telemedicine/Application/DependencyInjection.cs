using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.Telemedicine.Application;

/// <summary>
/// Registro de dependencias de la capa de aplicación: MediatR (CQRS) y
/// FluentValidation (pipeline). Los features se registran automáticamente por
/// ensamblado a medida que se agregan.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddTelemedicineApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(Application.Behaviors.ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
