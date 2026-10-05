namespace Brava.Api.Modules.Investments;

public record InvestmentDto(Guid Id, string Description, decimal Amount, DateTime Date);
