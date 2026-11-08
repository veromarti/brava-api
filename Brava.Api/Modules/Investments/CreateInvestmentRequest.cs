using Brava.Domain.Investments;

namespace Brava.Api.Modules.Investments;

public record CreateInvestmentRequest(string Description, InvestmentCategory Category, decimal Amount, DateTime Date);
