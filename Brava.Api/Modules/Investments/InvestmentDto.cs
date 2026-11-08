using Brava.Domain.Investments;

namespace Brava.Api.Modules.Investments;

public record InvestmentDto(Guid Id, string Description, InvestmentCategory Category, decimal Amount, DateTime Date);
