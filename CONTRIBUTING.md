# Contributing to Bitvavo.Net

Thanks for your interest in improving Bitvavo.Net. This library is part of the
[CryptoExchange.Net](https://github.com/JKorf/CryptoExchange.Net) exchange-client
ecosystem and follows its conventions throughout — contributions are expected to
match the patterns used by `Binance.Net`, `Kraken.Net`, `Bybit.Net`, etc.

## Prerequisites

- The .NET SDK for every targeted framework: **.NET 8**, **.NET 9**, and **.NET 10**.
  The library multi-targets `net8.0;net9.0;net10.0`, so all three SDKs must be
  installed for a full matrix build.
- Any IDE with .NET support (Visual Studio, Rider, or VS Code with the C# extension).

## Building

```bash
dotnet build Bitvavo.Net.csproj
```

A build must finish with **0 warnings and 0 errors** across all three target
frameworks before a change is considered ready.

## Running the tests

```bash
dotnet test Tests/Bitvavo.Net.Tests/Bitvavo.Net.Tests.csproj
```

The test suite uses xUnit v3, Shouldly, and NSubstitute. All tests must pass on
every target framework.

## Test-driven development

This project is developed test-first. For any new endpoint, model, or behaviour
change:

1. Write a failing test that captures the expected behaviour.
2. Confirm it fails (RED).
3. Implement the change.
4. Confirm the test — and all existing tests — pass (GREEN).

Bug fixes start with a failing test that reproduces the bug. Pull requests that
add behaviour without accompanying tests will be asked to add them.

## Code style

Match the conventions used across CryptoExchange.Net-based clients:

- Every public REST method returns `Task<WebCallResult<T>>`; every socket
  subscription returns `Task<CallResult<UpdateSubscription>>`. Protocol errors
  are surfaced through the result type — never thrown.
- Strongly typed request/response models; no loose dictionaries on the public
  surface.
- Options are configured through the `BitvavoRestOptions` / `BitvavoSocketOptions`
  configurators, and DI registration goes through `services.AddBitvavo()`.
- Public types and members carry XML documentation.
- Naming mirrors the sibling clients: `Bitvavo*RestClient.SpotApi.{ExchangeData,
  Account,Trading}` etc.

## Pull request flow

1. Fork the repository and create a topic branch.
2. Make your change test-first, keeping the build at 0 warnings / 0 errors.
3. Ensure the full test suite passes on all target frameworks.
4. Open a pull request describing the change and the motivation. Reference any
   related issue.
5. A maintainer reviews; address feedback and keep the branch up to date.

## Questions and discussion

For questions about the CryptoExchange.Net base library, dependency injection,
response processing, and the shared interfaces, see the
[CryptoExchange.Net documentation](https://cryptoexchange.jkorf.dev) and the
CryptoExchange.Net Discord linked from that site.
