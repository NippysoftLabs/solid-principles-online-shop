# SOLID principles in an online shop

This repository is the example code for a blog post on nippysoft.com:

- English: [SOLID principles with an online shop: what each one means and what guards it](https://nippysoft.com/en/blog/software-architecture/solid-principles-explained-with-an-online-shop)
- Español: [Principios SOLID con una tienda en línea: qué es cada uno y quién lo vigila](https://nippysoft.com/es/blog/arquitectura-de-software/principios-solid-explicados-con-una-tienda-en-linea)

**This is teaching code, not production code.** It is small on purpose so each idea fits on one screen. The cloud storage and the payment provider are in-memory stand-ins, there is no database, no web server and no security hardening, and the section "What this does not check" lists what the examples leave out. Read it to understand the principles; do not copy it into a real shop as it is.

## What it shows

SOLID is a set of five design principles for code that has to keep changing. Each letter is one principle:

- **S**ingle responsibility: each piece of code has one reason to change.
- **O**pen/closed: you add new behaviour without editing code that already works.
- **L**iskov substitution: anything that claims to implement a contract must behave the way callers of that contract expect.
- **I**nterface segregation: no consumer should depend on operations it does not use.
- **D**ependency inversion: business rules define the abstractions they need, and technical details implement them.

The repository shows each principle with the same online shop: orders, payments, shipping, the confirmation email and invoices. It adds a sixth rule that matters once the pieces are separate services: versioned data contracts.

Every principle has three parts:

1. A **"before"** design with a real problem, and a test that shows the problem. These tests pass because they assert the failure.
2. An **"after"** design that fixes it, with tests that show the fix.
3. **What guards the fix**: the compiler, a test, or nothing. Some "after" tests are that guard.

| Principle | Before (the failure the test shows) | After | What enforces it |
|---|---|---|---|
| Single responsibility | `OrderProcessor` charges the card, saves the order and writes the confirmation email. Marketing changes the greeting, and a customer called "Ana" sees an error page after the card was charged. | `Checkout` (sales: charge and save) in `Orders.Domain`; `ConfirmationEmail` (marketing) in `Orders.Notifications`, which reads saved orders. Even if the email code threw, the charge and the order would already be done. The two projects are a choice of this example; the principle asks for separate reasons to change, not for one project per responsibility. | Compiler: `Orders.Domain` does not reference `Orders.Notifications` (CS0234 in `samples/compile-fail`), and a reference back would be a cycle (MSB4006). Test: `ArchitectureTests`. |
| Open/closed | `ShippingCost` is a switch. "store-pickup" was added to the website but not to the switch, so it falls into the fallback and is charged 4.99 like standard shipping. | `ShippingCatalog` + `IShippingRule`. Two kinds of extension: a new option that uses an existing rule is one entry in `config/shipping.json` (store pickup); a new behaviour is a new `IShippingRule` class, and neither the catalog nor its consumer (`OrderTotals`) changes. An unknown option throws instead of taking another price. | Tests: `config/shipping.json` loads with strict JSON options and every option validates at startup; a rule written in the test assembly works with the compiled catalog; an unknown option and an option whose rule is not registered both fail. |
| Liskov substitution | `NaiveCentsGateway`, an adapter for a provider that works in cents: passes a happy-path test with 20.00, charges 19.00 for 19.99, throws on a declined card, lets a timeout escape and charges a retry twice. It breaks 12 of the 15 contract checks. | `CentsGateway`: same provider, keeps the contract of `IPaymentGateway`. A decline is `Declined`: the provider said no, nothing was charged, and the customer can try another card. No answer is `Unknown`: the card may have been charged, so the shop must not tell the customer it was declined; retrying the same order finds the charge if it happened. | Tests: one contract suite, `PaymentGatewayContract.Verify`, run against every gateway. |
| Interface segregation | `IOrderOperations` has five operations for three consumers. The customer's tracking page calls one, `GetStatus`, but depends on all five: its test double has to implement refunds and shipping, it receives an object that can refund, and a new finance operation breaks it. | `IOrderStatusReader` (tracking page), `IPickingQueue` (warehouse) and `IRefunds` (finance). One `OrderBook` class still implements all three. | Tests: each consumer depends on exactly the operations it calls. Compiler: `samples/compile-fail/FinanceAddsAnOperation` adds a finance operation to both designs; only the "before" tracking-page double and `OrderBook` fail (CS0535), the "after" double still compiles. |
| Dependency inversion | `InvoiceService` creates the vendor's cloud storage client itself. A second version takes the vendor's own interface through its constructor: injectable, but still shaped by the vendor (buckets, object paths). | `IInvoiceArchive`, declared by the business policy in `Orders.Domain` in its own terms; `CloudInvoiceArchive` in `Orders.Infrastructure` implements it. | Test: `ArchitectureTests` sets the rule, failing if the domain assembly references any assembly outside the framework (`System.*`); `DipTests` checks the port belongs to the domain and uses only framework types. Compiler: `Orders.Domain` has no project references (CS0246 in `samples/compile-fail`). |
| Versioned data contract | A consumer reads a renamed field as `0`, with no error. | Contract records read with required constructor parameters, strict numbers, no duplicate properties and enums as names; released schemas frozen in `tests/OnlineShop.Tests/Snapshots/order-placed/`. | Tests: each released type still produces its frozen schema; each release is compatible with the previous one or bumps the major version; the rename proposal is rejected as `1.2`. |

A complementary example sits next to interface segregation: one product response for every consumer sends the storefront `unitCost`, `supplierId` and `bin`; `StorefrontProduct` and `WarehouseProduct` give each consumer only its fields (`ApiResponseTests`). That is data minimization and API design, about responses rather than operations.

## How it is organized

```
src/FakeCloud.Storage       stand-in for a cloud vendor SDK, with the vendor's own ICloudBucket interface
src/Orders.Domain           checkout, payment and order ports, invoice service, IInvoiceArchive port (no project references)
src/Orders.Notifications    confirmation email (marketing); references Orders.Domain
src/Orders.Infrastructure   CloudInvoiceArchive adapter
src/Examples                before designs, shipping catalog, payment gateways and their contract suite,
                            order operations (ISP), product responses, contracts
config/shipping.json        the shipping options (open/closed)
tests/OnlineShop.Tests      one test class per principle, plus ApiResponseTests and ArchitectureTests
tools/HostileInputs         prints hostile inputs and their results
samples/compile-fail        code the compiler must reject
```

- `src/` holds the shop. The projects whose names start with `Orders.` are the "after" designs that need a project boundary; `src/Examples` holds the "before" designs and the "after" designs that do not.
- `tests/OnlineShop.Tests` has one test class per principle (`SrpTests`, `OcpTests`, `LspTests`, `IspTests`, `DipTests`, `ContractTests`), plus `ApiResponseTests` and `ArchitectureTests`.
- `tools/HostileInputs` and `samples/compile-fail` are explained in the next section.

## How to run it

You need the .NET 10 SDK; the exact version is pinned in `global.json`. Nothing else: no database, no network, no cloud account and no payment provider account.

```
git clone https://github.com/NippysoftLabs/solid-principles-online-shop.git
cd solid-principles-online-shop
dotnet test
```

The last line of `dotnet test` should report 44 passed and 0 failed.

Two more things you can run, from the repository root:

```
dotnet run --project tools/HostileInputs
dotnet build samples/compile-fail/CheckoutSendsEmail
dotnet build samples/compile-fail/DomainUsesVendor
dotnet build samples/compile-fail/FinanceAddsAnOperation
```

- `tools/HostileInputs` sends malformed or unusual inputs to the "after" code (broken JSON, odd amounts, odd names and ids) and prints what happens with each one. It is a way to see the limits of the examples, not part of the test run.
- Each `samples/compile-fail` project must fail to build: the error is the point, because it shows the compiler refusing a design the principle rules out. `samples/compile-fail/README.md` lists the expected errors. These projects are outside `SolidPrinciplesOnlineShop.slnx`, so `dotnet test` does not build them.

The same `dotnet test` runs in CI (`.github/workflows/test.yml`).

## The compiler and the tests, each for its part

- The compiler refuses code that uses a project the current project does not reference. It does not stop anyone from adding a `ProjectReference`, except one that closes a cycle.
- The architectural rule is set by `ArchitectureTests`, which reads the references the compiler kept in the domain assembly. Adding a reference to the vendor SDK and using it fails that test. Adding the reference without using it passes: the compiler drops unused references, and there is no coupling to report.

## What this does not check

- **What a value means.** The schema check compares types, properties, the required list, array items and enum values. Enum values appear in the schema only because `ContractJson` writes enums as names; with the default (numbers) a new member would not be reported. An amount that changes from euros to cents keeps the same type and passes. So do changes in `maxLength`, `pattern`, `format` or ranges.
- **Removing an optional property.** It is not reported: an old reader gets the default value. Whether that default is acceptable is a business decision.
- **Business rules in a contract.** `{"orderId":"","amount":-1}` is a valid `OrderPlaced`. Validate values where they are used.
- **Property name case.** Contracts are read with the Web defaults, which match names ignoring case: `{"OrderId":"o-1","AMOUNT":5}` is accepted. The schema check compares names the same way, so `amount` renamed to `Amount` is not reported.
- **Payments.** The contract suite checks each adapter against the stand-in provider in this repository. It shows the adapter keeps the contract when the provider behaves like the stand-in; it does not show a real provider behaves like that, which needs the same suite against the provider's test environment. A key or a card token made of spaces is accepted. Reusing a key with a different amount returns the first charge, without an error; some providers reject that instead. One currency (euros) only. Only `TimeoutException` and `IOException` count as "no answer"; a real SDK has its own exception types. After `Unknown`, the order is not saved until a retry finds the charge.
- **Interfaces.** Segregation limits what a consumer is given, not what it can reach: code that casts the tracking page's `IOrderStatusReader` to `OrderBook` can still refund.
- **Shipping.** Option names are matched exactly: "Store-Pickup" is an unknown option. An order total with more than 2 decimals is accepted by `Quote`.
- **The email.** `ConfirmationEmail.Render` returns plain text and does not escape the name: in an HTML email, escape it. An empty name gives "Hi , ..."; a null name throws.
- **Order ids.** `CloudInvoiceArchive` escapes the id so it stays one path segment, and rejects an id with a lone surrogate (escaping would turn it into U+FFFD and two ids would share one object). It does not reject an empty id (`invoices/.txt`) or a very long one.
- **Network contracts.** Everything runs in one process. Between services, the same checks belong in each side's CI (consumer-driven contract tests, schema registries).

## License

MIT. See `LICENSE`.
