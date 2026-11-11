# Code that must not compile

These projects are not in `SystemSolid.slnx`, so `dotnet test` never builds them. Build each one by hand to see the compiler refuse it:

```
dotnet build samples/compile-fail/InternalRounding
dotnet build samples/compile-fail/DomainUsesVendor
```

- `InternalRounding` references `Orders.Publishing` and calls `InvoiceRounding`, which is `internal`. Expected: error CS0122.
- `DomainUsesVendor` compiles the sources of `Orders.Domain` with the references that project has (none) plus one file that uses the vendor SDK. Expected: error CS0246.

The compiler only enforces the references a project declares. Adding a `ProjectReference` makes the second sample build; `tests/SystemSolid.Tests/ArchitectureTests.cs` is what fails then.
