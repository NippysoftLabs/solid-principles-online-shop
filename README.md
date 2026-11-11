# SOLID at system level, enforced by the compiler and by tests

A small .NET 10 example: the five SOLID principles applied to services, providers and data contracts, plus a sixth rule for versioned contracts. Each principle has a "before" design, an "after" design and tests. The "before" tests pass because they assert the failure: they show what goes wrong. The "after" tests show the fix, and some of them are the check that keeps the fix in place.

## Run it

```
dotnet test
```

Requires the .NET 10 SDK. No database, no network, no cloud account: the "cloud SDK" is a stand-in that keeps objects in memory.

## What each principle checks

| Principle | Before (the failure the test shows) | After | What enforces it |
|---|---|---|---|
| Single responsibility | `OrderPipeline`: finance changes invoice rounding to whole units and an order of 100.40 now passes a credit limit of 100. | `CreditPolicy` (domain) and `InvoicePublisher` (publishing) in separate projects; the rounding is `internal`. | Compiler: `InvoiceRounding` is not visible outside `Orders.Publishing` (CS0122, see `samples/compile-fail`). |
| Open/closed | `StockImporter`: a new source kind falls into the `default` arm and its rows are dropped, with no error. | `SourceCatalog` + `IRecordReader`: a tab-separated partner is one entry in `config/sources.json`. | Tests: `config/sources.json` loads with strict JSON options and every source validates; a bad row stops the import with its line number. |
| Liskov substitution | `NaiveFileBlobStore`: passes a happy-path test, breaks 5 of 14 contract checks on a case-insensitive file system with reserved names. | `HashedFileBlobStore`: file name is the SHA-256 of the key. | Tests: one contract suite, `BlobStoreContract.Verify`, run against every implementation. |
| Interface segregation | One product endpoint for every consumer: the storefront receives `unitCost`, `supplierId` and `bin`. | `StorefrontProduct` and `WarehouseProduct`, one contract per consumer. | Test: the storefront schema has exactly the allowlisted properties. |
| Dependency inversion | `InvoiceService` creates the vendor client itself; no constructor takes another provider. | `IInvoiceArchive` port in `Orders.Domain`, adapter in `Orders.Infrastructure`. | Compiler: `Orders.Domain` has no project references (CS0246 in `samples/compile-fail`). Test: `ArchitectureTests` fails if the domain assembly references the vendor, publishing or infrastructure. |
| Versioned data contract | A consumer reads a renamed field as `0`, with no error. | Contract records read with required constructor parameters, strict numbers and no duplicate properties; released schemas frozen in `tests/SystemSolid.Tests/Snapshots/order-placed/`. | Tests: each released type still produces its frozen schema; each release is compatible with the previous one or bumps the major version; the rename proposal is rejected as `1.2`. |

## The compiler and the tests, each for its part

- The compiler refuses code that uses a project the current project does not reference, or an `internal` type of another assembly. It does not stop anyone from adding a `ProjectReference`.
- `ArchitectureTests` reads the references the compiler kept in the domain assembly. Adding a reference to the vendor SDK and using it fails that test. Adding the reference without using it passes: the compiler drops unused references, and there is no coupling to report.
- `samples/compile-fail` holds two projects that must not compile. They are outside `SystemSolid.slnx`, so `dotnet test` does not build them.

## Hostile inputs

`dotnet run --project tools/HostileInputs` runs malformed or unusual inputs against the "after" code and prints each result: contract messages, schema pairs, version strings, source files, import rows, blob keys and order ids.

## What this does not check

- **What a value means.** The schema check compares types, properties, the required list, array items and enum values. An amount that changes from units to cents keeps the same type and passes. So do changes in `maxLength`, `pattern`, `format` or ranges.
- **Removing an optional property.** It is not reported: an old reader gets the default value. Whether that default is acceptable is a business decision.
- **Business rules in a contract.** `{"orderId":"","amount":-1}` is a valid `OrderPlaced`. Validate values where they are used.
- **Property name case.** Contracts are read with the Web defaults, which match names ignoring case: `{"OrderId":"o-1","AMOUNT":5}` is accepted.
- **The file system model.** `SimulatedFileSystem` applies the Win32 naming rules from Microsoft's documentation and a 255-character limit on names. It is not a real disk, and other systems (case-sensitive Linux file systems, cloud object stores) have other rules. The contract suite is what tells you whether a new store keeps the contract.
- **Order ids.** `CloudInvoiceArchive` escapes the id so it stays one path segment. It does not reject an empty id (`invoices/.txt`) or a very long one.
- **Network contracts.** Everything runs in one process. Between services, the same checks belong in each side's CI (consumer-driven contract tests, schema registries).

## Layout

```
src/FakeCloud.Storage       stand-in for a cloud vendor SDK
src/Orders.Domain           credit rule, invoice service, IInvoiceArchive port (no project references)
src/Orders.Publishing       invoice text, internal rounding
src/Orders.Infrastructure   CloudInvoiceArchive adapter
src/Examples                before designs, and the after designs that need no project boundary
config/sources.json         the import sources (open/closed)
tests/SystemSolid.Tests     one test class per principle, plus ArchitectureTests
tools/HostileInputs         prints hostile inputs and their results
samples/compile-fail        code the compiler must reject
```

## License

MIT. See `LICENSE`.
