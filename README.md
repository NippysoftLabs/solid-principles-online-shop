# SOLID at system level, enforced by the compiler and by tests

A small .NET 10 example built around an online shop: orders, payments, shipping, the confirmation email and invoices. It applies the five SOLID principles, plus a sixth rule for versioned data contracts. Each principle has a "before" design, an "after" design and tests. The "before" tests pass because they assert the failure: they show what goes wrong. The "after" tests show the fix, and some of them are the check that keeps the fix in place.

## Run it

```
dotnet test
```

Requires the .NET 10 SDK. No database, no network, no cloud account and no real payment provider: the "cloud SDK" and the "payment provider SDK" are stand-ins that keep everything in memory.

## What each principle checks

| Principle | Before (the failure the test shows) | After | What enforces it |
|---|---|---|---|
| Single responsibility | `OrderProcessor` charges the card, saves the order and writes the confirmation email. Marketing changes the greeting, and a customer called "Ana" sees an error page after the card was charged. | `Checkout` (sales: charge and save) in `Orders.Domain`; `ConfirmationEmail` (marketing) in `Orders.Notifications`, which reads saved orders. Even if the email code threw, the charge and the order would already be done. | Compiler: `Orders.Domain` does not reference `Orders.Notifications` (CS0234 in `samples/compile-fail`), and a reference back would be a cycle (MSB4006). Test: `ArchitectureTests`. |
| Open/closed | `ShippingCost` is a switch. "store-pickup" was added to the website but not to the switch, so it is charged 4.99 like standard shipping. | `ShippingCatalog` + `IShippingRule`: store pickup is one entry in `config/shipping.json`; a new kind of price is a new class, and the catalog does not change. | Tests: `config/shipping.json` loads with strict JSON options and every option validates at startup; an unknown option throws instead of taking another price. |
| Liskov substitution | `NaiveCentsGateway`, an adapter for a provider that works in cents: passes a happy-path test with 20.00, charges 19.00 for 19.99, throws on a declined card and charges a retry twice. It breaks 10 of the 13 contract checks. | `CentsGateway`: same provider, keeps the contract of `IPaymentGateway`, including that a declined charge does not use up the key, so the customer can pay the same order with another card. | Tests: one contract suite, `PaymentGatewayContract.Verify`, run against every gateway. |
| Interface segregation | One product endpoint for every consumer: the storefront receives `unitCost`, `supplierId` and `bin`. | `StorefrontProduct` and `WarehouseProduct`, one contract per consumer. | Test: the storefront schema has exactly the allowlisted properties. |
| Dependency inversion | `InvoiceService` creates the vendor's cloud storage client itself; no constructor takes another provider. | `IInvoiceArchive` port in `Orders.Domain`, adapter in `Orders.Infrastructure`. | Compiler: `Orders.Domain` has no project references (CS0246 in `samples/compile-fail`). Test: `ArchitectureTests` fails if the domain assembly references any assembly outside the framework (`System.*`). |
| Versioned data contract | A consumer reads a renamed field as `0`, with no error. | Contract records read with required constructor parameters, strict numbers, no duplicate properties and enums as names; released schemas frozen in `tests/SystemSolid.Tests/Snapshots/order-placed/`. | Tests: each released type still produces its frozen schema; each release is compatible with the previous one or bumps the major version; the rename proposal is rejected as `1.2`. |

## The compiler and the tests, each for its part

- The compiler refuses code that uses a project the current project does not reference. It does not stop anyone from adding a `ProjectReference`, except one that closes a cycle.
- `ArchitectureTests` reads the references the compiler kept in the domain assembly. Adding a reference to the vendor SDK and using it fails that test. Adding the reference without using it passes: the compiler drops unused references, and there is no coupling to report.
- `samples/compile-fail` holds two projects that must not compile. They are outside `SystemSolid.slnx`, so `dotnet test` does not build them.

## Hostile inputs

`dotnet run --project tools/HostileInputs`, from the repository root, runs malformed or unusual inputs against the "after" code and prints each result: contract messages, schema pairs, version strings, shipping files, shipping quotes, payments (reference, naive and fixed gateways), customer names in the email and order ids.

## What this does not check

- **What a value means.** The schema check compares types, properties, the required list, array items and enum values. Enum values appear in the schema only because `ContractJson` writes enums as names; with the default (numbers) a new member would not be reported. An amount that changes from euros to cents keeps the same type and passes. So do changes in `maxLength`, `pattern`, `format` or ranges.
- **Removing an optional property.** It is not reported: an old reader gets the default value. Whether that default is acceptable is a business decision.
- **Business rules in a contract.** `{"orderId":"","amount":-1}` is a valid `OrderPlaced`. Validate values where they are used.
- **Property name case.** Contracts are read with the Web defaults, which match names ignoring case: `{"OrderId":"o-1","AMOUNT":5}` is accepted. The schema check compares names the same way, so `amount` renamed to `Amount` is not reported.
- **Payments.** The contract suite checks the inputs it lists. A key or a card token made of spaces is accepted. Reusing a key with a different amount returns the first charge, without an error; some providers reject that instead. One currency (euros) only. The stand-in provider is not a real one: a real adapter also has to handle timeouts, where you do not know whether the card was charged.
- **Shipping.** Option names are matched exactly: "Store-Pickup" is an unknown option. An order total with more than 2 decimals is accepted by `Quote`.
- **The email.** `ConfirmationEmail.Render` returns plain text and does not escape the name: in an HTML email, escape it. An empty name gives "Hi , ..."; a null name throws.
- **Order ids.** `CloudInvoiceArchive` escapes the id so it stays one path segment, and rejects an id with a lone surrogate (escaping would turn it into U+FFFD and two ids would share one object). It does not reject an empty id (`invoices/.txt`) or a very long one.
- **Network contracts.** Everything runs in one process. Between services, the same checks belong in each side's CI (consumer-driven contract tests, schema registries).

## Layout

```
src/FakeCloud.Storage       stand-in for a cloud vendor SDK
src/Orders.Domain           checkout, payment and order ports, invoice service, IInvoiceArchive port (no project references)
src/Orders.Notifications    confirmation email (marketing); references Orders.Domain
src/Orders.Infrastructure   CloudInvoiceArchive adapter
src/Examples                before designs, the shipping catalog, the payment gateways and their contract suite, products, contracts
config/shipping.json        the shipping options (open/closed)
tests/SystemSolid.Tests     one test class per principle, plus ArchitectureTests
tools/HostileInputs         prints hostile inputs and their results
samples/compile-fail        code the compiler must reject
```

## License

MIT. See `LICENSE`.
