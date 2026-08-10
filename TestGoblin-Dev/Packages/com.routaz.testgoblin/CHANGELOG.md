# Changelog

All notable changes to this package are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

The package stops being an AltTester wrapper and becomes a keyword-driven test
framework with an in-process default driver. Several changes are breaking; see
**Migration** below.

### Added
- **`InProcessGameDriver` — the new default.** Drives a live uGUI scene from inside
  play mode via the event system. No licence, no TCP, no external process, so the
  suite runs free and headless in CI. A tap is rejected unless the target is
  interactable *and* is what a pointer at its own position would actually hit, so a
  control hidden behind an overlay fails instead of silently "working".
- **Scenario layer** — `ScenarioActionAttribute`, `KeywordRegistry`, `Scenario`,
  `ScenarioLoader` and `ScenarioExecutor`. Methods marked `[ScenarioAction("Name")]`
  become keywords that JSON scenarios compose; the attribute pins the name so C#
  refactors cannot silently break saved scenarios.
- **`ScenarioRunnerTests`** — the one test the package ships. Not a test *of*
  TestGoblin: it enumerates a consuming project's `TestGoblin/Scenarios~/*.json` and
  yields one NUnit test case per file, with no code generation and no compile step.
- **`KeywordCatalogExporter`** — writes `keywords.json` (names, parameters, types,
  defaults, enum values) for tooling to render. Menu item, batch-mode entry point,
  or direct call.
- **`GameDriverFactory`** — the config-to-driver mapping, previously buried in a test
  base class. This is what makes "switching targets changes config, never code" real.
- **`ExecutionTarget.Fake`** — there was previously no way to *ask* for the fake
  driver, so `Editor` had to double as "no game". `Editor` now means play mode.
- **`FakeGameDriver` and `FakeBackendTestApi` now ship**, in `Runtime/`. They are a
  driver and a backend implementation, not test scaffolding, and a consumer needs
  them to test their own Screens with no game attached.
- Dependency on `com.unity.ugui`, which the in-process driver requires.

### Changed
- **Breaking — `IGameDriver.Connect()` takes no arguments.** It previously took a
  device id, host and port, which meant the core abstraction named a specific
  vendor's needs. Connection details now come from the config a driver is
  constructed with.
- **Breaking — `TestRunConfig.AltTesterHost`/`AltTesterPort` are now
  `DriverHost`/`DriverPort`**; the env-var overrides `TESTGOBLIN_ALT_TESTER_HOST`
  and `TESTGOBLIN_ALT_TESTER_PORT` become `TESTGOBLIN_DRIVER_HOST` and
  `TESTGOBLIN_DRIVER_PORT`.
- **Breaking — `ConfigLoader` no longer requires a driver host.** The default driver
  has nothing to connect to; the driver that needs a host validates its own config.
- **Breaking — the package no longer contains game-specific code.** Screens and Flows
  modelling a particular game are per-title code and belong in the consuming project.
  The package ships `ScreenBase` and the driver/scenario machinery, and never a
  screen that knows a button's name.
- **Breaking — TestGoblin's own tests no longer ship.** They live in the development
  host project and exercise the package's public surface as a consumer would. Adding
  the package to `testables` therefore gives you the scenario runner and nothing else.

### Removed
- **Breaking — `TutorialScreen`, `GameBoardScreen`, `DailyRewardPopup` and
  `GameFlows`.** These modelled a fictional match-3 game; they are per-title code.
- **Breaking — `BaseTest`.** Its useful part (config-driven driver selection) is now
  `GameDriverFactory`; the rest was fixture-specific.
- **The AltTester SDK dependency.** `AltTesterGameDriver` remains behind
  `#if ALTTESTER_PRESENT` as a worked example of the out-of-process tier, but nothing
  compiles it. An MIT package linking GPL-3 assemblies for a tier it cannot exercise
  was carrying a licensing question it did not need to answer.

### Fixed
- **`Waits` no longer uses `ConfigureAwait(false)`.** Discarding the synchronization
  context resumed polls on a threadpool thread, so any driver touching Unity APIs
  worked on the first poll and threw on every one after — a defect that reads as
  flakiness. The captured context is required, which is also why fixtures using the
  in-process driver must be play mode.
- **`InProcessGameDriver` now yields a frame after `Tap` and `Drag`.** Every other
  operation completes synchronously, so a whole scenario could run inside one frame
  and the game never got to respond. uGUI rebuilds canvas geometry at end of frame,
  so a button a tap had just revealed was not yet registered with the raycaster and
  reported as "nothing is hit-testable". Simulating input and letting the game react
  to it are two different things, and the driver owes callers both.

### Migration
- Replace `driver.Connect(deviceId, host, port)` with `driver.Connect()`, and pass
  configuration to the driver's constructor.
- Rename `altTesterHost`/`altTesterPort` in config JSON to `driverHost`/`driverPort`,
  and update any `TESTGOBLIN_ALT_TESTER_*` environment variables.
- Move Screens and Flows into your own assembly. Nothing needs to reference them:
  keywords are found by scanning loaded assemblies for `[ScenarioAction]`.
- Replace `BaseTest` with your own fixture base, using `GameDriverFactory.Create(config)`
  for driver selection.
- Add `"testables": ["com.routaz.testgoblin"]` to your `Packages/manifest.json` so the
  scenario runner is discovered, and put scenarios in `<project>/TestGoblin/Scenarios~/`.

### Known limitations
- `AltTesterGameDriver` is an uncompiled example. It was verified by the compiler
  against SDK 2.3.2 before the SDK was removed, and can now rot unnoticed.
- `InProcessGameDriver` is uGUI-specific and play-mode only. A UI Toolkit game needs a
  different `IGameDriver`; nothing above the seam changes.
- The in-process tier **cannot detect input-stack misconfiguration.** It dispatches
  through `ExecuteEvents` and raycasts via `EventSystem` directly, so it never touches
  the input module — during development it drove a demo game perfectly while that
  game's input was misconfigured in a way a player would have hit immediately.
- The package has never been installed into another project. Everything is verified
  inside the repository it was written in.
- The real HTTP `IBackendTestApi` is still unimplemented.

## [0.1.0] - 2026-07-16

First tagged release. The framework core is complete and green against an
in-memory fake driver; the AltTester path compiles against the real SDK (2.3.2)
but its live run is pending a licensed AltTester Desktop connection.

### Added
- **Config layer** — `TestRunConfig` (immutable, `init`-only) and `ConfigLoader`
  (JSON load → `TESTGOBLIN_<FIELD>` env-var overrides → fail-fast validation).
- **Driver seam** — `IGameDriver` abstraction, the `GoblinObject` value handle,
  and `Waits` (the single polling implementation; no `Thread.Sleep` above the
  driver layer).
- **Drivers** — `FakeGameDriver` (in-memory, for device-free tests) and
  `AltTesterGameDriver` (backed by AltTester, gated behind `#if ALTTESTER_PRESENT`
  so the package stays usable without the SDK).
- **Screens & Flows** — `ScreenBase`, `TutorialScreen`, `GameBoardScreen`,
  `DailyRewardPopup`, and `GameFlows`, with locator discipline (object names as
  private consts).
- **Base test class** — `BaseTest` with config-driven driver/backend creation,
  per-test state reset, and artifact-capture hooks on failure.
- **Backend seam** — `IBackendTestApi` (pin A/B variants, seed player state, read
  inventory, snapshot config, reset) with an in-memory fake implementation.
- **Tests** — config, fake-driver (waits/timeouts/tap/drag), and two
  state-asserting end-to-end tests (tutorial completion, daily-reward claim).
- **CI** — example two-tier GitHub Actions workflow (PR smoke / nightly device).
- **Compatibility** — `IsExternalInit` shim enabling `init` accessors on Unity 6's
  target framework.

### Known limitations
- The live AltTester demo (play-mode test driving the instrumented `DemoGame`
  scene) is written but not yet exercised — it requires an AltTester Desktop
  license to connect.
- `AltTesterGameDriver` and the real HTTP `IBackendTestApi` are not yet validated
  against a running device/backend.

[Unreleased]: https://github.com/routaz/TestGoblin/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/routaz/TestGoblin/releases/tag/v0.1.0
