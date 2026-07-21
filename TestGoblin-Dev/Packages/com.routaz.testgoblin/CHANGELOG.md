# Changelog

All notable changes to this package are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.1.0]: https://github.com/routaz/TestGoblin/releases/tag/v0.1.0
