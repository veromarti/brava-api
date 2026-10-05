namespace Brava.Api.Modules.Investments;

public record CreateInvestmentRequest(string Description, decimal Amount, DateTime Date);
