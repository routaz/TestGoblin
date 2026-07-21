# TestGoblin - Proof of concept for a Unity test-automation framework

A small, layered **UI + gameplay test-automation framework for Unity**, built
around [AltTester](https://alttester.com/) and Unity's Test Framework. It targets
a fictional match-3 mobile game, but the framework itself is
game-agnostic: the game-specific parts are deliberately isolated so the core can
be shared across multiple projects.

TestGoblin ships as a UPM package (`com.routaz.testgoblin`) developed inside a
Unity 6 host project (`TestGoblin-Dev`).

> **Status:** framework core is complete and green against an in-memory fake
> driver. The AltTester path is implemented and compiles against the real SDK
> (2.3.2); the final live-scene run is pending a licensed AltTester Desktop
> connection. See [What's actually verified](#whats-actually-verified) for the
> full proven-vs-pending breakdown.

---

## Overview

Automated tests for a live Unity game need to do three things:

* **Drive the UI**
    * tap, drag, swipe on real scene objects
  
* **Wait for the game to settle**
  * without flaky fixed sleeps

* **Assert on state**
  * inventory, progress, A/B variant — things the UI alone can't reveal.

TestGoblin provides a clean seam for all three, and lets you write and run the
entire test suite **without a game or device attached** by swapping in a fake
driver.

The design goal throughout: tests read like plain-English intent
(`flow.ClaimDailyReward()`).

---

## Architecture

Three layers, each depending only on the one below through an interface:

```
┌─────────────────────────────────────────────────────────┐
│  Tests            NUnit fixtures : Arrange/Act/Assert     │
│                   (FtueTests, DailyRewardTests, …)        │
├─────────────────────────────────────────────────────────┤
│  Screens / Flows  Page-Object layer. Intent-level verbs   │
│                   (TutorialScreen.CompleteStep(),         │
│                    GameFlows.PlayUntilEnergyFull())        │
├─────────────────────────────────────────────────────────┤
│  Driver           IGameDriver : the one abstraction       │
│                   everything waits/taps/reads through.     │
│                   ├── AltTesterGameDriver  (real, stub)   │
│                   └── FakeGameDriver       (in-memory)    │
└─────────────────────────────────────────────────────────┘
         │                                   │
         ▼                                   ▼
   Waits.cs (single polling                IBackendTestApi
   implementation, timeout policy)         (seed / read / pin state)
```

**Why the `IGameDriver` seam is the heart of it:**

- **Swappability** — AltTester is wrapped, never referenced above the driver. If
  its API, pricing, or availability changes, only `AltTesterGameDriver` changes.
- **Testability without a device** — `FakeGameDriver` implements the same
  interface entirely in memory, which is how the suite runs green in CI with no
  game, emulator, or SDK.
- **Multi-target execution** — Editor / standalone player / real device all go
  through one interface; only `TestRunConfig.ExecutionTarget` picks which
  implementation is constructed.

**Waits live in exactly one place.** `Waits.WaitForCondition(...)` is the only
polling loop in the codebase. Because waiting is a driver-level verb, every layer
above is declarative — a screen reads `await Driver.WaitForObject(ClaimButton)`,
never a timing loop.

---

## Tool choice & justification

| Tool | Role here | Why / why not |
|---|---|---|
| **AltTester** | **Primary in-engine driver** | Sees the Unity scene graph — find objects by name/path/component, read properties, simulate taps/drags. Embeds a small server into a debug build; tests talk to it over TCP. Has a C# client that drops straight into NUnit. |
| Appium | Native-shell only (out of scope) | A Unity game renders into a single native GL surface — Appium sees one `UnityPlayerActivity` and nothing inside it. Useful for OS-level dialogs, store flows, permissions; not for in-game objects. |
| Unity Test Framework | Test runner + edit/play-mode host | Runs the NUnit suite. Complementary, not competing — UTF runs the tests; AltTester drives the game. |
| Unity GameDriver / others | — | Same category as AltTester; wrapping behind `IGameDriver` means one could be swapped in without touching tests. |

---

## Cost & licensing

AltTester's free SDK is **GPL-3**, which is unsuitable for a closed-source
commercial game — shipping it in a proprietary build would trigger GPL
obligations. A paid subscription grants a non-GPL license, so it's effectively
**mandatory for a commercial title regardless of budget**. A free "Lite" tier
exists only for very small studios, so most commercial studios must pay.

---

## How TestGoblin hooks into Unity

1. **Instrumented debug build** — the game is built with the AltTester SDK
   embedded (debug/test builds only, never shipping builds). This starts a small
   TCP server inside the running game.
2. **Connection** — the test process connects to that server. Tests can then query the live scene graph.
3. **Distribution** — TestGoblin is consumed as a UPM package via git URL. A
   consuming project adds one line to `Packages/manifest.json`:

   ```json
   "com.routaz.testgoblin": "https://github.com/<you>/TestGoblin.git?path=/TestGoblin-Dev/Packages/com.routaz.testgoblin"
   ```

   and adds it to `"testables"` so the test runner discovers the suite.

Because the whole framework talks to the game through `IGameDriver`, the same
tests can run **inside Unity** (edit/play-mode) or as a **plain NUnit project
outside Unity** against a built game — the seam is what keeps both doors open.

---

## How to run

The suite is config-driven: one `TestRunConfig`, loaded from JSON with per-field
environment-variable overrides (`TESTGOBLIN_<FIELD>`). Switching targets changes
config, never code.

- **Local (editor, fake driver)** — open `TestGoblin-Dev` in Unity 6, open
  `Window > General > Test Runner`, run the EditMode tests. This uses
  `FakeGameDriver` and needs no game, device, or SDK. This is what CI's PR tier runs.
- **Standalone player** — set `ExecutionTarget=StandalonePlayer` (via config or
  `TESTGOBLIN_EXECUTION_TARGET`); intended to run an instrumented player under a
  driver connection. *(Stubbed — see verified section.)*
- **Real device** — set `ExecutionTarget=Device`, provide `TESTGOBLIN_ALT_TESTER_HOST`
  etc.; nightly tier. *(Stubbed.)*

Config lookup: `TESTGOBLIN_CONFIG_PATH` if set, else
`Packages/com.routaz.testgoblin/config.local.json` relative to the project root.

---

## Execution tiers

Two tiers, same suite, differing only by config (see `ci/github-actions-example.yml`):

| Tier | Trigger | Target | Cost | Catches |
|---|---|---|---|---|
| **PR smoke** | every pull request | editor / fake driver | ~free, no device farm | logic, wiring, wait/timeout regressions |
| **Nightly device** | schedule | real device via provider | device-farm cost | touch quirks, notches, permissions, perf, real backend |

**Honest caveat:** the cheap editor/fake tier validates framework logic and flow
composition — it does **not** validate touch input fidelity, device-specific UI
(notches, safe areas), OS permission dialogs, or performance. Those only surface
on the device tier. The two tiers are complementary; neither alone is sufficient.

---

## What's actually verified

| Piece | Status |
|---|---|
| `FakeGameDriver` — waits, timeouts, tap/drag recording, object scripting | ✅ **Proven** — green edit-mode NUnit tests |
| Config load + env-var override + validation | ✅ **Proven** — tested |
| Screens / Flows driving the fake end-to-end (FTUE + daily-reward) | ✅ **Proven** — both tests assert *state* via the backend seam, green |
| Backend seam (`IBackendTestApi`) | ✅ Fake impl proven; real HTTP impl stubbed |
| `AltTesterGameDriver` (real AltTester path) | ✅ **Compiles against the real SDK (2.3.2)** — every AltTester API call verified by the compiler; gated behind `#if ALTTESTER_PRESENT` so the package stays usable without the SDK. Not yet exercised live (see below). |
| Live demo scene (framework driving a real uGUI scene) | ⚠️ **Built + instrumented** — a uGUI demo scene exists, is AltTester-instrumented, and the framework is wired to drive it. The live run is **gated on an AltTester Desktop license** (the 2.x broker requires one to connect). |
| Device / standalone execution tiers | ⛔ **Stubbed** in CI |

This section is deliberately blunt about the one gap that remains: the AltTester
driver is real, compiled code (not a guess) and the demo scene is built and
instrumented — but AltTester 2.x routes the connection through AltTester Desktop,
which needs a license, so the final "watch it click a real button" run is pending
that. Everything up to the licensed connection is done and verified.

The `IGameDriver` seam is what makes the fake driver meaningful — the code under
test is identical regardless of which driver runs, which is also why the AltTester
path could be built and compiled with confidence before ever connecting live.

---

## Maintainability as the game grows

- **Locator discipline** — every object name is a `private const` inside its
  Screen, marked `// TODO(verify)` where guessed. UI rename ⇒ one line changes.
- **Screens own UI; Flows are the stable API** — tests call intent verbs
  (`ClaimDailyReward()`), never raw locators. Refactoring UI doesn't touch tests.
- **Framework core vs. per-title code** — this is the key scaling line: the
  **driver, waits, config, backend seam** are shared package code; the
  **Screens, Flows, and test data** modelling a specific game are per-title. Here
  they live together for simplicity, but in a real multi-title org the Screens/Flows
  move into each title's own project, depending on the shared TestGoblin package.
- **Version pinning** — titles pin a TestGoblin version and upgrade deliberately.
  No forced breaking changes across many titles from one framework edit.
- **Config-driven targets** — the same suite runs on any tier by changing
  `TestRunConfig`, never code.

---

## Assumptions

- A debug/test build of the game is instrumented with the AltTester SDK; shipping
  builds are not.
- The tester machine / CI runner can reach the game's AltTester port (localhost in
  editor; `adb forward` on device).
- Object names used as locators are guesses until confirmed against a real
  instrumented build (every one is marked `TODO(verify)`).
- Unity 6 (6000.0). Older versions are untested and not claimed.
- A real backend test API exists in staging for seeding/reading player state; only
  the in-memory fake is implemented here.

---

## TODOs & roadmap

- Install the AltTester trial SDK and make `AltTesterGameDriver` real.
- A demo uGUI scene the framework drives live (the "proof level 2" artifact).
- Real HTTP-backed `IBackendTestApi`.
- `[Category("Smoke")]` tagging so tiers run different subsets.

See [`docs/roadmap.md`](docs/roadmap.md) for the fuller list.
