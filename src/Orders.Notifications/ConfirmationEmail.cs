using Orders.Domain;

namespace Orders.Notifications;

/// <summary>
/// The confirmation email, owned by the marketing team. It receives an order that checkout has already
/// charged and saved, and renders the text; it does not read the order store or send anything itself.
/// Checkout never calls it. Even if this code threw, the charge and the order would already be done and
/// the customer completing the sale would not see the error. Separating the code does not guarantee the
/// email is sent: nothing in this repository sends it (see "Delivering the email" in the README).
/// </summary>
public static class ConfirmationEmail
{
    public static string Render(Order order) =>
        $"Hi {FirstName(order.CustomerName)}, your order {order.Id} is confirmed.";

    // Marketing's rule: greet by the first word of the name. A name with no space ("Ana") is kept whole.
    internal static string FirstName(string fullName)
    {
        var name = fullName.Trim();
        var space = name.IndexOf(' ');
        return space < 0 ? name : name[..space];
    }
}
