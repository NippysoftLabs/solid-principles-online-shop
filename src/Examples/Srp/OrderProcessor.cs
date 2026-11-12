using Orders.Domain;

namespace Examples.Srp.Before;

/// <summary>
/// BEFORE: one class charges the card, saves the order and writes the confirmation email.
/// Sales owns the first two steps and marketing owns the email, but both edit this method.
/// </summary>
public sealed class OrderProcessor(IPaymentGateway payments, IOrderStore orders)
{
    public List<string> SentEmails { get; } = [];

    public void Place(Order order, string cardToken)
    {
        var payment = payments.Charge(order.Id, cardToken, order.Total);
        if (payment.Status == PaymentStatus.Declined)
        {
            throw new InvalidOperationException("card declined");
        }
        orders.Save(order);

        // Marketing's change: greet by first name, cut at the first space. For "Ana" IndexOf
        // returns -1 and the slice throws, after the card was charged and the order saved.
        var firstName = order.CustomerName[..order.CustomerName.IndexOf(' ')];
        SentEmails.Add($"Hi {firstName}, your order {order.Id} is confirmed.");
    }
}
