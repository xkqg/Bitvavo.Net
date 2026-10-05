# Contributing to Bitvavo.Net

Thanks for your interest in improving Bitvavo.Net. This library is part of the
[CryptoExchange.Net](https://github.com/JKorf/CryptoExchange.Net) exchange-client
ecosystem and follows its conventions throughout — contributions are expected to
match the patterns used by `Binance.Net`, `Kraken.Net`, `Bybit.Net`, etc.
[ARCHITECTURE.md](ARCHITECTURE.md) explains how the library is put together and why.

## Prerequisites

- The .NET SDK for every targeted framework: **.NET 8**, **.NET 9**, and **.NET 10**.
  The library multi-targets `net8.0;net9.0;net10.0`, so all three SDKs must be
  installed for a full matrix build.
- Any IDE with .NET support (Visual Studio, Rider, or VS Code with the C# extension).

## Building

```bash
dotnet build Bitvavo.Net.slnx
```

A build must finish with **0 warnings and 0 errors** across all three target
frameworks before a change is considered ready (CI builds Release with
`-warnaserror`; that includes vulnerable-package advisories and the example projects).

## Running the tests

```bash
dotnet run --project Bitvavo.Net.Tests --framework net10.0   # also net8.0 and net9.0
```

The test project is an xunit v3 executable: `dotnet run` runs the tests, `dotnet test`
does not. The suite uses xUnit v3 and Shouldly. All tests must pass on every target
framework.

`BitvavoLivePublicTests` call the live public API (no credentials, a handful of requests) and
are skipped unless `BITVAVO_LIVE_PUBLIC=1` is set — run them when a change touches how
responses are read. Signed endpoints are never called by the test suite.

The only test seam is the network transport — `StubHttpMessageHandler` for REST,
`RecordingSocketFactory` / CryptoExchange.Net's `TestSocket` for WebSockets — fed with
payloads shaped like Bitvavo's documentation. The library's own types are never
replaced by test doubles, and rate limiting is never switched off.

## Test-driven development

This project is developed test-first. For any new endpoint, model, or behaviour
change:

1. Write a failing test that captures the expected behaviour.
2. Confirm it fails (RED).
3. Implement the change.
4. Confirm the test — and all existing tests — pass (GREEN).

Bug fixes start with a failing test that reproduces the bug. Pull requests that
add behaviour without accompanying tests will be asked to add them. A new REST
endpoint also needs a row in `BitvavoEndpointContracts`; a test fails without it.

## Code style

Match the conventions used across CryptoExchange.Net-based clients:

- Every public REST method returns `Task<HttpResult<T>>`; every socket
  subscription returns `Task<WebSocketResult<UpdateSubscription>>`. Protocol errors
  are surfaced through the result type — never thrown.
- Strongly typed request/response models; no loose dictionaries on the public
  surface.
- Options are configured through the `BitvavoRestOptions` / `BitvavoSocketOptions` /
  `BitvavoOptions` configurators, and DI registration goes through `services.AddBitvavo()`.
- Public types and members carry XML documentation, in English.
- Braces on every `if` / `else` / loop body.
- No static `*Helper` / `*Util` classes: a shared function is an extension method on
  the type it acts on, a shared abstraction a generic base class.
- No tuples: a multi-value result is a `readonly record struct`.
- Naming mirrors the sibling clients: `Bitvavo*RestClient.SpotApi.{ExchangeData,
  Account,Trading}` etc.

### Extending the Shared API

A Shared API capability is one partial file
(`BitvavoRestClientSpotSharedApi.<Capability>.cs`) that declares its interface(s) and
holds its options and its call; its interface goes into the aggregate interface
(`IBitvavoRestClientSpotApiShared.cs`) and its options into `SetCapabilities`.
`BitvavoSharedApiRegistrationTests` fails when those three disagree.

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
