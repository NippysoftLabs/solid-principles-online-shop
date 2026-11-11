using System.Globalization;
using System.Text;

namespace Orders.Domain;

public sealed class InvoiceService(IInvoiceArchive archive)
{
    public string Archive(Order order)
    {
        var document = $"{order.Id};{order.Customer};{order.Amount.ToString(CultureInfo.InvariantCulture)}";
        return archive.Save(order.Id, Encoding.UTF8.GetBytes(document));
    }
}
