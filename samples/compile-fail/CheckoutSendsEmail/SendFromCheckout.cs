using Orders.Notifications;

namespace Orders.Domain;

// Someone adds the confirmation email back into the sale, inside the domain.
public static class SendFromCheckout
{
    public static string Email(Order order) => ConfirmationEmail.Render(order);
}
