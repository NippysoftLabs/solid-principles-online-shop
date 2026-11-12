# Code that must not compile

These projects are not in `SolidPrinciplesOnlineShop.slnx`, so `dotnet test` never builds them. Build each one by hand to see the compiler refuse it:

```
dotnet build samples/compile-fail/CheckoutSendsEmail
dotnet build samples/compile-fail/DomainUsesVendor
dotnet build samples/compile-fail/FinanceAddsAnOperation
```

- `CheckoutSendsEmail` compiles the sources of `Orders.Domain` with the references that project has (none) plus one file that calls marketing's `ConfirmationEmail`. Expected: error CS0234 (the namespace `Orders.Notifications` does not exist for this project).
- `DomainUsesVendor` compiles the same sources plus one file that uses the vendor SDK. Expected: error CS0246.
- `FinanceAddsAnOperation` compiles the interface segregation example (`src/Examples/Isp/OrderOperations.cs`) plus a finance change, `PartialRefund`, added to both designs, and one tracking-page test double per design. Expected: error CS0535 three times: for the "before" double (`FatTrackingDouble`) and twice for `OrderBook`, the one class that does implement refunds. The "after" double (`StatusTrackingDouble`) has no error: the change does not reach it.

The compiler only enforces the references a project declares. If someone adds a `ProjectReference` from `Orders.Domain` to the vendor SDK and uses it, the build passes and `tests/OnlineShop.Tests/ArchitectureTests.cs` is what fails. A `ProjectReference` from `Orders.Domain` to `Orders.Notifications` does not get that far: `Orders.Notifications` already references `Orders.Domain`, and MSBuild stops on the cycle (error MSB4006).
