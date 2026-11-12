namespace Orders.Domain;

/// <summary>
/// Owned by the sales team: charge the card and record the order. The confirmation email belongs
/// to marketing and lives in Orders.Notifications, which this project does not reference.
/// </summary>
public sealed class Checkout(IPaymentGateway payments, IOrderStore orders)
{
    public CheckoutResult Place(Order order, string cardToken)
    {
        // The order id is the idempotency key: with a gateway that keeps the contract, a retry of the
        // same order returns the first charge instead of charging again.
        var payment = payments.Charge(order.Id, cardToken, order.Total);
        switch (payment.Status)
        {
            case PaymentStatus.Approved:
                orders.Save(order);
                return CheckoutResult.Placed;
            case PaymentStatus.Declined:
                return CheckoutResult.Declined;
            default:
                // Unknown, or a status this code does not know: never report it as declined.
                return CheckoutResult.PaymentUnknown;
        }
    }
}

/// <summary>
/// Placed: charged and saved. Declined: nothing was charged; the customer can try another card.
/// PaymentUnknown: the provider did not answer; the order is not saved yet, and the customer should
/// not pay again with another card. Retrying the same order is safe: it reuses the same key.
/// </summary>
public enum CheckoutResult { Placed, Declined, PaymentUnknown }
