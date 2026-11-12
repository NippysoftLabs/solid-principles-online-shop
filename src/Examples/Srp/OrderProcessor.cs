using Orders.Domain;

namespace Examples.Srp.Before;

/// <summary>
/// BEFORE: one class charges the card, saves the order and writes the confirmation email.
/// Sales owns the first two steps and marketing owns the email, but both edit this method.
/// The payment handling is right on purpose: the problem this example shows is the mixed
/// responsibilities, not a payment bug.
/// </summary>
public sealed class OrderProcessor(IPaymentGateway payments, IOrderStore orders)
{
    public List<string> SentEmails { get; } = [];

    public CheckoutResult Place(Order order, string cardToken)
    {
        var payment = payments.Charge(order.Id, cardToken, order.Total);
        switch (payment.Status)
        {
            case PaymentStatus.Approved:
                break;
            case PaymentStatus.Declined:
                return CheckoutResult.Declined;
            default:
                // Unknown (or a status this code does not know): we do not know whether it was paid, so the
                // order is not saved as paid and no confirmation goes out.
                return CheckoutResult.PaymentUnknown;
        }
        orders.Save(order);

        // Marketing's change: greet by first name, cut at the first space. For "Ana" IndexOf
        // returns -1 and the slice throws, after the card was charged and the order saved.
        var firstName = order.CustomerName[..order.CustomerName.IndexOf(' ')];
        SentEmails.Add($"Hi {firstName}, your order {order.Id} is confirmed.");
        return CheckoutResult.Placed;
    }
}
