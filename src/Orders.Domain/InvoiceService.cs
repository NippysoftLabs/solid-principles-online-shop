using System.Globalization;
using System.Text;

namespace Orders.Domain;

/// <summary>Writes the invoice of an order and hands it to whatever archive it was given.</summary>
public sealed class InvoiceService(IInvoiceArchive archive)
{
    public string Archive(Order order)
    {
        var document = $"{order.Id};{order.CustomerName};{order.Total.ToString(CultureInfo.InvariantCulture)}";
        return archive.Save(order.Id, Encoding.UTF8.GetBytes(document));
    }
}
