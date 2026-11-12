using Orders.Domain;

namespace Orders.Notifications;

/// <summary>
/// The confirmation email, owned by the marketing team. It reads an order that checkout already
/// charged and saved; checkout never calls it, so a mistake here cannot stop a sale.
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
