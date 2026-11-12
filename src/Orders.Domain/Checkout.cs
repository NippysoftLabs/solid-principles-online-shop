namespace Orders.Domain;

/// <summary>
/// Owned by the sales team: charge the card and record the order. The confirmation email belongs
/// to marketing and lives in Orders.Notifications, which this project does not reference.
/// </summary>
public sealed class Checkout(IPaymentGateway payments, IOrderStore orders)
{
    /// <summary>Returns true when the card was charged and the order saved, false when the card was declined.</summary>
    public bool Place(Order order, string cardToken)
    {
        // The order id is the idempotency key: with a gateway that keeps the contract, a retry of the
        // same order returns the first charge instead of charging again.
        var payment = payments.Charge(order.Id, cardToken, order.Total);
        if (payment.Status == PaymentStatus.Declined)
        {
            return false;
        }
        orders.Save(order);
        return true;
    }
}
