using FluentValidation;

namespace CoppAddresd.Application.Features.Inventory;

public sealed class CreateEntryRequestValidator : AbstractValidator<CreateEntryRequest>
{
    public CreateEntryRequestValidator()
    {
        RuleFor(request => request.Date).NotEmpty();
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(60);
        RuleFor(request => request.Lines).NotEmpty();
        RuleForEach(request => request.Lines).SetValidator(new EntryLineValidator());
    }
}

public sealed class CreateExitRequestValidator : AbstractValidator<CreateExitRequest>
{
    public CreateExitRequestValidator()
    {
        RuleFor(request => request.Date).NotEmpty();
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(60);
        RuleFor(request => request.Lines).NotEmpty();
        RuleForEach(request => request.Lines).SetValidator(new ExitLineValidator());
    }
}

public sealed class EntryLineValidator : AbstractValidator<InventoryEntryLineInput>
{
    public EntryLineValidator()
    {
        RuleFor(line => line.ProductId).NotEmpty();
        RuleFor(line => line.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(line => line.Quantity).GreaterThan(0);
        RuleFor(line => line.Lot).NotEmpty().MaximumLength(50);
        RuleFor(line => line.UnitCost).GreaterThanOrEqualTo(0);
    }
}

public sealed class ExitLineValidator : AbstractValidator<InventoryExitLineInput>
{
    public ExitLineValidator()
    {
        RuleFor(line => line.ProductId).NotEmpty();
        RuleFor(line => line.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(line => line.Quantity).GreaterThan(0);
        RuleFor(line => line.Lot).NotEmpty().MaximumLength(50);
        RuleFor(line => line.UnitCost).GreaterThanOrEqualTo(0);
    }
}
