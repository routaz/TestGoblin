# TestGoblin — a Unity test-automation framework manual QA can drive

A layered **UI + gameplay test-automation framework for Unity**, built on Unity's
Test Framework and designed so that **test cases can be composed without opening
the Unity editor**. It targets a fictional match-3 mobile game, but the framework
itself is game-agnostic: the game-specific parts are deliberately isolated so the
core can be shared across multiple projects.

TestGoblin ships as a UPM package (`com.routaz.testgoblin`) developed inside a
Unity 6 host project (`TestGoblin-Dev`), plus a small local control server in
[`tools/`](tools/) that runs the suite and reports results in a browser.

> **Status:** the framework drives a real uGUI scene in play mode with no licence
> and no external process (1 play-mode test), and is green against an in-memory fake
> driver (18 edit-mode tests). The control server works end to end — it launches Unity
> headless, parses the results, and renders them in a browser. The keyword layer that
> lets manual QA author scenarios is **designed but not yet built**. The AltTester
> device tier is now an **uncompiled example**, not a dependency.
> See [What's actually verified](#whats-actually-verified) — it is deliberately blunt.

---

## The problem

Automated tests for a live Unity game need to do four things:

* **Drive the UI** — tap, drag, swipe on real scene objects
* **Wait for the game to settle** — without flaky fixed sleeps
* **Assert on state** — inventory, progress, A/B variant; things the UI alone can't reveal
* **Be writable by the people who know the test cases** — which is usually manual QA, not the automation engineer

The first three are what most Unity test frameworks address. The fourth is the one
TestGoblin is actually about, and it's the reason the whole architecture is shaped
the way it is.

The design goal throughout: tests read like plain-English intent
(`flow.ClaimDailyReward()`), because a vocabulary of intent verbs is the only kind
that a non-programmer can safely compose.

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  Control server + browser UI        tools/TestGoblin.Server  │
│  Launch runs, parse results, (later) author scenarios         │
│  Plain .NET — deliberately OUTSIDE the Unity project          │
└──────────────────────────────────────────────────────────────┘
                    │ Unity -batchmode -runTests
                    ▼
┌──────────────────────────────────────────────────────────────┐
│  Tests            NUnit fixtures : Arrange/Act/Assert         │
│                   FtueTests, DailyRewardTests, …              │
│                   ScenarioRunnerTests  ← planned              │
├──────────────────────────────────────────────────────────────┤
│  Screens / Flows  Page-Object layer. Intent-level verbs.      │
│                   TutorialScreen.CompleteStep()               │
│                   GameFlows.CompleteTutorial()                │
│                   ← also the keyword vocabulary               │
│                   ⚠ PER-TITLE CODE — lives in the game's      │
│                     project, NOT in the package               │
├──────────────────────────────────────────────────────────────┤
│  Driver           IGameDriver : the one abstraction that      │
│                   everything waits / taps / reads through.    │
│                   ├── InProcessGameDriver   (uGUI)       ✅    │
│                   ├── PhysicsGameDriver     (2D sprites) ✅    │
│                   ├── FakeGameDriver        (in-memory)  ✅    │
│                   └── AltTesterGameDriver   (example, uncompiled) │
└──────────────────────────────────────────────────────────────┘
         │                                    │
         ▼                                    ▼
   Waits.cs (single polling                IBackendTestApi
   implementation, timeout policy)        (seed / read / pin state)
```

**Why the `IGameDriver` seam is the heart of it:**

- **Swappability** — no driver SDK is ever referenced above the driver layer. If an
  API, price, or licence changes, exactly one class changes.
- **Testability without a device** — `FakeGameDriver` implements the same interface
  entirely in memory, which is how the suite runs green in CI with no game, emulator
  or SDK at all.
- **Multi-target execution** — one scenario, three drivers. `TestRunConfig.ExecutionTarget`
  picks the implementation; nothing above the seam notices.

**Waits live in one place.** `Waits.WaitForCondition(...)` is the only *timing* loop in
the codebase — because waiting is a driver-level verb, every layer above is declarative:
a screen reads `await Driver.WaitForObject(ClaimButton)`, never a timing loop. There is
no `Thread.Sleep` anywhere in the package.

One known exception: `GameFlows.PlayUntilEnergyFull()` loops on game state with no
timeout, so a game that never reaches the target state produces a hung test rather than
a failed one. It is marked as a TODO, not presented as finished.

---

## The keyword layer — the point of the project

> **Built.** A scenario can be composed in a browser, saved, and run as its own named
> Unity test — no C#, no editor. What remains untested is *distribution*: the package
> has never been installed into another project. Stated up front because this section
> explains every other decision on this page.

A manual QA engineer should be able to build a test case out of named actions in a
browser, save it, and have it appear as a real Unity test in CI — without opening
Unity, without writing C#, and without waiting for a compile.

The mechanism:

1. **Keywords are opt-in via attribute** — `[ScenarioAction("CompleteTutorial")]` on
   methods in `GameFlows`. An attribute rather than raw reflection over method names,
   so the keyword string survives C# refactors and private helpers never leak into the
   vocabulary.
2. **Keywords are intent-level, not primitive** — `CompleteTutorial`, `ClaimDailyReward`;
   never `Tap` or `Drag`. See the honest limitation below.
3. **A catalog is exported** — `keywords.json` lists each keyword with its parameters
   and types, so the browser UI can render the right input for each argument.
4. **Scenarios are data, never generated code** — a scenario is a JSON list of steps in
   `<project>/TestGoblin/Scenarios~/`. A single `[TestCaseSource]` fixture enumerates that folder and yields
   **one NUnit test case per scenario file**. No `.cs` generation, no domain reload, no
   compile step between "QA hits save" and "the test runs".

**The honest limitation.** Keyword-driven testing has a well-known failure mode: the
vocabulary is either too small to express real test cases, or it grows conditionals,
loops and variables until the JSON is a bad programming language and everyone would
have been better off writing C#. Keeping keywords at intent level is the defence, but
it has a real cost — **extending the vocabulary requires an engineer**. TestGoblin
does not pretend otherwise. This tool makes QA independent for *composition*, not for
*capability*.

---

## Tool choice & justification

| Tool | Role here | Why / why not |
|---|---|---|
| **Unity Test Framework** | **The execution engine** | Runs the NUnit suite in edit mode, play mode, or headless batch mode. Free, already present, produces standard NUnit3 XML that CI understands. This is the tier everything is developed against. |
| **In-process driver** | **Default driver** | Runs inside play mode and drives the live scene directly through uGUI's event system. No licence, no TCP, no external app — so the whole suite runs free and headless on a plain CI VM. |
| AltTester | Worked example, not a dependency | Sees the Unity scene graph from *outside* the process — the right tool for a real build on real hardware, which an in-process driver fundamentally cannot test. The SDK has been removed from the project; `AltTesterGameDriver` remains behind `#if ALTTESTER_PRESENT` to show what that tier looks like. |
| Appium | Native-shell only (out of scope) | A Unity game renders into a single native GL surface — Appium sees one `UnityPlayerActivity` and nothing inside it. Useful for OS dialogs, store flows, permissions; not for in-game objects. |
| Unity GameDriver / others | — | Same category as AltTester. Wrapping behind `IGameDriver` means one could be swapped in without touching a single test. |

---

## Cost & licensing

AltTester's free SDK is **GPL-3**, which is unsuitable for a closed-source commercial
game — shipping it in a proprietary build would trigger GPL obligations. A paid
subscription grants a non-GPL licence, making it effectively **mandatory for a
commercial title regardless of budget**. Cost scales with **seats plus a parallel-connection
add-on**, not with the number of titles; for a large nightly regression suite the
parallelism add-on is the number to watch.

This is precisely why the **default** driver is in-process. The paid tier buys
something real — testing an actual build on actual hardware — but it should not be
on the critical path for every pull request. Making it the default would mean no
tests run at all until someone buys a licence.

The SDK has since been removed from this project entirely. An MIT-licensed package
that links GPL-3 assemblies for a tier it cannot exercise is carrying a licensing
question it does not need to answer. `AltTesterGameDriver` survives as an example of
how the out-of-process tier would be written — see the honest cost of that in
[What's actually verified](#whats-actually-verified).

---

## How to run

### The control server (primary)

Double-click [`tools/testgoblin.cmd`](tools/testgoblin.cmd), or:

```bash
dotnet run --project tools/TestGoblin.Server/TestGoblin.Server.csproj
```

It opens http://127.0.0.1:5111 in your browser, runs the suite on a button press,
and renders the parsed results. It binds to loopback only — it launches processes on
your machine and must not be reachable from the network.

It also **exits by itself a few seconds after you close the tab**, so no server is
left running in the background. Liveness is a held server-sent-events connection
rather than a heartbeat timer, because browsers throttle timers in hidden tabs and a
backgrounded-but-open tab would otherwise look closed. Closing the tab *during* a run
lets the run finish first rather than orphaning a Unity process.

> Unity locks a project while the editor has it open. **Close the Unity editor before
> running a batch-mode run**, or the run will hang. The server checks for the lockfile
> and fails fast with that message rather than making you wait to find out.

Configuration lives in [`appsettings.json`](tools/TestGoblin.Server/appsettings.json):
Unity's editor path, the project path, and the shutdown timings.

### Inside Unity

Open `TestGoblin-Dev` in Unity 6 and use `Window > General > Test Runner`. The
EditMode tests use `FakeGameDriver` and need no game, device or SDK.

### CI

The same batch-mode command the server runs, with `Server:ExitWhenIdle` irrelevant
because no server is involved. See [`ci/github-actions-example.yml`](ci/github-actions-example.yml).

The suite itself is config-driven: one `TestRunConfig`, loaded from JSON with per-field
environment-variable overrides (`TESTGOBLIN_<FIELD>`). Switching targets changes
config, never code. Lookup order: `TESTGOBLIN_CONFIG_PATH` if set, else
`Packages/com.routaz.testgoblin/config.local.json`.

---

## Execution tiers

Two tiers, same suite, differing only by config:

| Tier | Trigger | Target | Cost | Catches |
|---|---|---|---|---|
| **PR smoke** | every pull request | editor / in-process | ~free, no device farm | logic, wiring, wait/timeout regressions, scenario validity |
| **Nightly device** | schedule | real device via provider | licence + device-farm cost | touch quirks, notches, permissions, perf, real backend |

**Honest caveat:** the cheap tier validates framework logic and flow composition. It
does **not** validate touch input fidelity, device-specific UI (notches, safe areas),
OS permission dialogs, or performance. Those only surface on the device tier. The two
tiers are complementary; neither alone is sufficient.

One caveat is worth naming concretely, because it was demonstrated rather than guessed:
`InProcessGameDriver` dispatches through `ExecuteEvents` and raycasts via `EventSystem`
directly, so **it never touches the input module**. During development it drove the demo
game perfectly while that game's input stack was misconfigured — a player would have hit
the failure instantly. An in-process driver cannot verify that input is wired up at all.

---

## What's actually verified

| Piece | Status |
|---|---|
| `FakeGameDriver` — waits, timeouts, tap/drag recording, object scripting | ✅ **Proven** — green edit-mode NUnit tests |
| Config load + env-var override + validation | ✅ **Proven** — tested |
| Screens / Flows driving the fake end-to-end (FTUE + daily reward) | ✅ **Proven** — both assert *state* via the backend seam |
| Unity headless batch-mode run | ✅ **Proven** — `-runTests -batchmode`, exit 0, 18/18 |
| Control server: launch Unity, parse NUnit3 XML, serve results | ✅ **Proven** — verified via API and in a browser |
| Server self-shutdown when the UI closes | ✅ **Proven** — verified for tab-closed, tab-held, and closed-mid-run (run completes first) |
| UI state recovery on reload | ✅ **Proven** — reloading during a run resumes it; after one, restores results |
| Backend seam (`IBackendTestApi`) | ✅ Fake proven; real HTTP impl **stubbed** |
| **`InProcessGameDriver` driving the live `DemoGame` scene** | ✅ **Proven** — play-mode test completes the tutorial through real pointer events and asserts the board appears and the overlay is gone. No licence, no external process. |
| Tap fidelity (interactable + raycast preconditions) | ✅ Implemented — a tap fails if the target is disabled or covered, rather than invoking the handler directly |
| `AltTesterGameDriver` | ⚠️ **Example only — not compiled.** It was compiler-verified against SDK 2.3.2 before the SDK was removed; it is now inert code behind `#if ALTTESTER_PRESENT` that nothing builds or runs, and it can rot without anything noticing. |
| Keyword discovery (`[ScenarioAction]` + `KeywordRegistry`) | ✅ **Proven** — 11 tests, mostly covering what it *refuses*: duplicate names, non-`Task` returns, unrepresentable parameter types |
| `keywords.json` catalog export | ✅ **Proven** — runs headless via `-executeMethod`; excludes test assemblies so fixtures never reach QA's vocabulary |
| Scenario format + `ScenarioExecutor` | ✅ **Proven** — 11 tests, most asserting on *error message content*, because the person reading them cannot read the C# |
| Scenario → NUnit test case | ✅ **Proven** — `ftue.scenario.json` runs headless as `Scenario("ftue.scenario.json")`, no generated code |
| Scenario builder UI | ✅ **Proven** — a scenario authored in the browser was saved to disk and ran as its own test. The delete *button* is unverified; its endpoint is |
| Running scenarios from the browser | ✅ **Proven** — suite selector runs EditMode or PlayMode; unknown platforms are rejected with 400 |
| Failure reporting to a non-programmer | ✅ **Proven** — a failing step reports scenario name, step number, action, cause and file path |
| Device / standalone execution tiers | ⛔ **Stubbed** |
| Package contains no game-specific code | ✅ **Proven** — Screens, Flows and TestGoblin's own tests moved to the host project; keywords in an unreferenced assembly are still discovered and still run |
| Config → driver mapping (`GameDriverFactory`) | ✅ **Proven** — 5 tests covering every tier plus the unmapped case. This is what makes "switching targets changes config, never code" real rather than aspirational |
| Polling waits against a real, time-taking transition | ✅ **Proven** — the demo app's login → loading → game transition takes 1.5s, and `LoginFlowTests` asserts the wait *actually waited*. Every other state change in the project is same-frame, so a driver that never waited would have passed them all |
| Driving a real game (spawn, drag-to-merge, conditional buttons) | ✅ **Proven** — a scenario logs in, plays to a level-up and takes it, entirely through simulated input |
| A second UI technology behind the same seam (`PhysicsGameDriver`) | ✅ **Proven** — 4 tests drive 2D sprites with colliders, including refusing a sprite covered by another at identical depth. Screens, Flows and scenarios above the seam are unchanged |
| Package install into a fresh project via git URL | ⛔ **Never tested.** The known blockers are fixed — `testables` declared, scenarios moved into the project, package purged of game code — but nothing has actually been installed anywhere yet. |

Read that table as the real state of the project. The framework now drives a real
scene, the tooling half works, and the headline feature — the keyword layer — is
still entirely ahead.

Two honest downgrades are recorded above rather than quietly dropped: the AltTester
driver lost its compiler verification when the SDK was removed, and the package has
never been installed anywhere other than the project it was written in.

The `IGameDriver` seam is what makes the fake driver meaningful: the code under test is
identical regardless of which driver runs. Adding `InProcessGameDriver` required no
logic change to any screen, flow or test — the same `GameFlows.CompleteTutorial()` that
passes against the fake now passes against a live scene.

It did require two fixes, and both are worth recording because they are the kind of
defect a fake driver structurally cannot catch:

- **A wrong locator.** `GameBoardScreen` looked for an object named `GameBoardScreen`;
  the scene calls it `GameBoard`. A `TODO(verify)` that stayed unverified because no
  test had ever touched a real scene.
- **A threading bug.** `Waits` used `ConfigureAwait(false)`, discarding the Unity
  synchronization context, so every poll after the first resumed off the main thread.
  The fake never noticed — it only touches a dictionary. Against a real scene this
  would have passed whenever an object was already present and thrown whenever it had
  to wait, which reads as flakiness rather than a bug.

---

## Maintainability as the game grows

- **Locator discipline** — every object name is a `private const` inside its Screen,
  marked `// TODO(verify)` where guessed. A UI rename means one line changes.
- **Screens own UI; Flows are the stable API** — tests call intent verbs
  (`ClaimDailyReward()`), never raw locators. Refactoring UI doesn't touch tests.
- **Framework core vs. per-title code** — the key scaling line, and it is enforced by
  the repo layout rather than described in prose. The **driver, waits, config, backend
  seam and keyword machinery** are shared package code. The **Screens, Flows and test
  data** modelling a specific game are per-title, and live in the host project
  (`Assets/Scripts/GameTestSupport/`, assembly `DemoGame.TestSupport`). Nothing in the
  package knows a button's name.
  <br>The proof this actually works: `GameFlows` sits in an assembly the package has no
  reference to, and its keywords still appear in the catalog — found by domain scan
  rather than by being named. A consuming game's flows assembly is discovered the same
  way, without TestGoblin knowing it exists.
- **TestGoblin's own tests are not shipped.** They live in the host project and
  exercise the package's public surface exactly as a consumer would. The only test in
  the package is `ScenarioRunnerTests`, which is not a test *of* TestGoblin but the
  fixture that turns a consumer's scenarios into tests.
- **Version pinning** — titles pin a TestGoblin version and upgrade deliberately. One
  framework edit cannot break many titles at once.
- **Config-driven targets** — the same suite runs on any tier by changing
  `TestRunConfig`, never code.

---

## Assumptions

- Unity 6 (6000.0+). Older versions are untested and not claimed.
- Batch-mode runs require the Unity editor to have the project closed.
- The default path needs no instrumented build and no SDK.
- **Fixtures using `InProcessGameDriver` must be play mode.** Its waits rely on the
  captured synchronization context marshalling continuations back to Unity's main
  thread, and that needs a pumping frame loop. Edit mode has none — which is why the
  fake-driver tier exists and why `Waits` must never use `ConfigureAwait(false)`.
- The in-process driver is **uGUI-specific**. A UI Toolkit game needs a different
  `IGameDriver`; nothing above the seam changes.
- Object names used as locators are guesses until confirmed against a real build —
  every one is marked `TODO(verify)`.
- A real backend test API exists in staging for seeding and reading player state;
  only the in-memory fake is implemented here.
- The control server is a local developer tool: loopback-bound, no authentication,
  run history held in memory and lost on restart.

---

## Roadmap

Immediate, in order:

1. ~~`InProcessGameDriver`~~ — done, drives the `DemoGame` scene in play mode.
2. ~~`[ScenarioAction]` + `KeywordRegistry` + `keywords.json` export~~ — done.
3. ~~Scenario JSON format, `ScenarioExecutor`, and the `[TestCaseSource]` runner~~ — done.
4. ~~Let the control server run PlayMode~~ — done.
5. ~~Scenario builder UI in the control server~~ — done.
6. **Install the package into a fresh project via git URL.** The two anticipated
   blockers are now fixed — `testables` is declared, and scenarios live in
   `<project>/TestGoblin/Scenarios~` rather than inside the package — but the install
   itself has still never been performed, so expect it to surface more.
5. Install the package into a fresh project via git URL and confirm the suite is
   discovered — the real test of whether this is a distributable tool.

Later: real HTTP-backed `IBackendTestApi`; `[Category("Smoke")]` tagging so tiers run
different subsets; bounding `GameFlows.PlayUntilEnergyFull()` so a stuck game fails
instead of hanging; a licensed AltTester device run.

See [`docs/roadmap.md`](docs/roadmap.md) for the fuller list.
