---
paths:
  - "**/*Tests/**"
  - "**/*Benchmarks/**"
---

# Testing & benchmarking

Loads when working in a test or benchmark project. General C# standards still
apply — see root `CLAUDE.md`.

## Testing

- **Framework: xUnit** (the de-facto .NET standard).
- **Mocking: NSubstitute** (free, open source).
- **Assertions: Shouldly** (free, open source) — fluent, readable failure
  messages.
- **The test project mirrors the source feature-slice layout exactly** — same
  feature slices, same type-based subfolders. Finding the tests for a thing is the
  same navigation as finding the thing.
- **Data-driven by default when a method has more than one meaningful case.**
  Use `[Theory]` with the cleanest data source (`[InlineData]` for simple cases,
  `TheoryData<T>` for type-safe richer ones). One method, many scenarios.
- **Naming: `Method_Scenario_ExpectedResult`.** Arrange-Act-Assert, one logical
  focus per test.

## What earns tests in a hobby project

Test effort goes where bugs are silent and data is at stake, in this order:

1. **Derived-state logic** (to buy / backlog / behind computations) — pure,
   cheap to test, and the whole UI depends on them being right. Full
   `TheoryData` coverage of the read/owned/released matrix.
2. **Import matching** (StoryGraph title → AniList candidate scoring) — the
   fuzzy logic lives or dies on edge cases; encode every mismatch found during
   the real import as a regression case.
3. **Sync upsert idempotency** — running the same sync twice changes nothing;
   sync never mutates user state. These bugs corrupt data quietly.
4. **External payload mapping** — dirty → clean mapping against captured real
   AniList/MangaUpdates responses stored as test fixtures (`Fixtures/` in the
   slice), not hand-invented JSON.

UI components and controllers are verified by using the app — don't write
ceremony tests for them.

## Benchmarking

- **Flag, don't guess.** When there's more than one reasonable implementation, or
  any genuine uncertainty about performance, say so and ask whether a benchmark is
  worth writing — then measure. Don't silently pick the "probably faster" option.
- **Tool: BenchmarkDotNet.**
- **Baseline + ratio.** Mark the reference implementation
  `[Benchmark(Baseline = true)]` so the comparison is a ratio, not raw numbers in
  a vacuum.
- **Always measure memory** — add `[MemoryDiagnoser]`.
- Benchmarks are investigative artefacts, kept out of the normal test run. Run
  with `dotnet run -c Release --project <BenchmarkProject>`.
- Realistically this project needs zero benchmarks; the rule exists so that if
  one ever seems warranted, the bar is "measure, don't vibe."
