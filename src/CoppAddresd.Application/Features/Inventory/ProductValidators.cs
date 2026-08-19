using FluentValidation;
using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Application.Features.Inventory;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().WithMessage("El SKU es requerido.").MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es requerido.").MaximumLength(200);
        RuleFor(x => x.ProductType).NotEmpty().WithMessage("El tipo de producto es requerido.")
            .Must(ProductTypes.IsValid).WithMessage("Tipo de producto no válido.");
        RuleFor(x => x.Category).NotEmpty().WithMessage("La categoría es requerida.").MaximumLength(100);
        RuleFor(x => x.Presentation).NotEmpty().WithMessage("La presentación es requerida.").MaximumLength(60);
        RuleFor(x => x.Unit).NotEmpty().WithMessage("La unidad es requerida.").MaximumLength(60);
        RuleFor(x => x.Status).Must(ProductStatuses.IsValid).WithMessage("Estado no válido.");
        RuleFor(x => x.ExpirationDate)
            .Must(date => date.HasValue && date.Value.Date >= DateTime.Today)
            .WithMessage("La fecha de vencimiento no puede ser en el pasado.")
            .When(x => x.ExpirationDate.HasValue);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaximumStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().WithMessage("El SKU es requerido.").MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es requerido.").MaximumLength(200);
        RuleFor(x => x.ProductType).NotEmpty().WithMessage("El tipo de producto es requerido.")
            .Must(ProductTypes.IsValid).WithMessage("Tipo de producto no válido.");
        RuleFor(x => x.Category).NotEmpty().WithMessage("La categoría es requerida.").MaximumLength(100);
        RuleFor(x => x.Presentation).NotEmpty().WithMessage("La presentación es requerida.").MaximumLength(60);
        RuleFor(x => x.Unit).NotEmpty().WithMessage("La unidad es requerida.").MaximumLength(60);
        RuleFor(x => x.Status).Must(ProductStatuses.IsValid).WithMessage("Estado no válido.");
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaximumStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
    }
}
