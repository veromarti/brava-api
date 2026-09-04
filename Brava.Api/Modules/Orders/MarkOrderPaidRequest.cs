using Brava.Domain.Orders;

namespace Brava.Api.Modules.Orders;

public record MarkOrderPaidRequest(PaymentMethod PaymentMethod);
