namespace Examples.Isp.Before
{
    // Finance asks for partial refunds, in the one interface everybody uses.
    public partial interface IOrderOperations
    {
        void PartialRefund(string orderId, decimal amount);
    }
}

namespace Examples.Isp.After
{
    // The same request, in the interface only finance uses.
    public partial interface IRefunds
    {
        void PartialRefund(string orderId, decimal amount);
    }
}
