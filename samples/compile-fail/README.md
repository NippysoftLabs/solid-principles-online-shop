# Code that must not compile

These projects are not in `SystemSolid.slnx`, so `dotnet test` never builds them. Build each one by hand to see the compiler refuse it:

```
dotnet build samples/compile-fail/CheckoutSendsEmail
dotnet build samples/compile-fail/DomainUsesVendor
```

- `CheckoutSendsEmail` compiles the sources of `Orders.Domain` with the references that project has (none) plus one file that calls marketing's `ConfirmationEmail`. Expected: error CS0234 (the namespace `Orders.Notifications` does not exist for this project).
- `DomainUsesVendor` compiles the same sources plus one file that uses the vendor SDK. Expected: error CS0246.

The compiler only enforces the references a project declares. If someone adds a `ProjectReference` from `Orders.Domain` to the vendor SDK and uses it, the build passes and `tests/SystemSolid.Tests/ArchitectureTests.cs` is what fails. A `ProjectReference` from `Orders.Domain` to `Orders.Notifications` does not get that far: `Orders.Notifications` already references `Orders.Domain`, and MSBuild stops on the cycle (error MSB4006).
