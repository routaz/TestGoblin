# TestGoblin — a Unity test-automation framework manual QA can drive

> ### ⚠️ This is a vibe-coded proof of concept
>
> TestGoblin was built conversationally with an AI assistant over a handful of
> sessions, as a personal learning project. It is not a product, it has no users,
> and it has never been installed into a project other than the one it was written
> in. The code is real, the tests genuinely pass, and the design decisions are
> argued rather than guessed — but treat it as a working sketch of an idea, not as
> something to put in front of a shipping title without reading every line first.
>
> The [What's actually verified](#whats-actually-verified) table is deliberately
> blunt about which claims on this page are demonstrated and which are not.

---

TestGoblin is a layered **UI and gameplay test-automation framework for Unity**,
built on the Unity Test Framework, designed so that **test cases can be composed
without opening the Unity editor and without writing C#**.

It ships as a UPM package (`com.routaz.testgoblin`) developed inside a Unity 6 host
project (`TestGoblin-Dev`), with three importable samples — one per input technology —
and a small local control server in [`tools/`](tools/) that runs the suite and reports
results in a browser.

---

## Contents

- [The problem](#the-problem)
- [Architecture](#architecture)
- [The keyword layer — the point of the project](#the-keyword-layer--the-point-of-the-project)
- [Drivers](#drivers)
- [Configuration](#configuration)
- [Quick start](#quick-start)
- [Samples](#samples)
- [The control server](#the-control-server)
- [Adopting TestGoblin in your own game](#adopting-testgoblin-in-your-own-game)
- [Execution tiers and CI](#execution-tiers-and-ci)
- [What's actually verified](#whats-actually-verified)
- [Tool choice and licensing](#tool-choice-and-licensing)
- [Assumptions and limitations](#assumptions-and-limitations)
- [Repo layout](#repo-layout)
- [Roadmap](#roadmap)

---

## The problem

Automated tests for a live Unity game need to do four things:

* **Drive the UI** — tap and drag real scene objects the way a player would
* **Wait for the game to settle** — without flaky fixed sleeps
* **Assert on state** — inventory, progress, A/B variant; things the UI alone can't reveal
* **Be writable by the people who know the test cases** — which is usually manual QA, not the automation engineer

The first three are what most Unity test frameworks address. The fourth is the one
TestGoblin is actually about, and it is the reason the whole architecture is shaped
the way it is.

The design goal throughout: tests read like plain-English intent (`PlayUntilLevelUpReady`,
`UseItemOn`), because a vocabulary of intent verbs is the only kind a non-programmer
can safely compose.

---

## Architecture

```
┌───────────────────────────────────────────────────────────────────────┐
│  Control server + browser UI                    tools/TestGoblin.Server│
│  Author scenarios, launch runs, parse and render results               │
│  Plain .NET — deliberately OUTSIDE the Unity project                   │
└───────────────────────────────────────────────────────────────────────┘
                    │ Unity -batchmode -runTests
                    ▼
┌───────────────────────────────────────────────────────────────────────┐
│  Scenarios        JSON files → one NUnit test case each                │
│                   ScenarioRunnerTests  (the only test the pkg ships)   │
├───────────────────────────────────────────────────────────────────────┤
│  Screens / Flows  Page-Object layer. Intent-level verbs.               │
│                   [ScenarioAction] marks the keyword vocabulary        │
│                   ⚠ PER-TITLE CODE — lives in the game's project,      │
│                     never in the package                               │
├───────────────────────────────────────────────────────────────────────┤
│  Driver           IGameDriver : the one abstraction that everything    │
│                   waits / taps / drags / reads through                 │
│                   ├── InProcessGameDriver  (uGUI)                      │
│                   ├── PhysicsGameDriver    (2D sprites + colliders)    │
│                   ├── Physics3DGameDriver  (3D meshes + colliders)     │
│                   ├── UiToolkitGameDriver  (UIDocument / VisualElement)│
│                   ├── CompositeGameDriver  (a stack of the above)      │
│                   ├── FakeGameDriver       (in-memory, no game at all) │
│                   └── AltTesterGameDriver  (example, uncompiled)       │
└───────────────────────────────────────────────────────────────────────┘
         │                                    │
         ▼                                    ▼
   Waits.cs (the only polling            IBackendTestApi
   loop; timeout policy)                (seed / read / pin state)
```

### Why `IGameDriver` is the heart of it

`IGameDriver` is deliberately tiny — ten members, no vendor types:

```csharp
Task Connect();
Task Disconnect();
Task<GoblinObject> WaitForObject(string name, int id = 0);
Task WaitForObjectGone(string name, int id = 0);
Task WaitForCondition(Func<Task<bool>> condition, float timeout, int pollIntervalMs, string description);
Task<GoblinObject> FindObject(string name, int id = 0);
Task Tap(GoblinObject goblinObject);
Task Drag(GoblinObject sourceObject, GoblinObject destinationObject);
Task<string> GetText(GoblinObject goblinObject);
```

- **Swappability** — no driver SDK is referenced above the driver layer. If an API,
  price or licence changes, exactly one class changes.
- **Testability without a game** — `FakeGameDriver` implements the same interface
  entirely in memory, which is how a large part of the suite runs green in edit mode
  with no scene, emulator or SDK.
- **Multi-technology execution** — the same scenario, screen and flow code runs against
  uGUI, 2D sprites, 3D meshes or UI Toolkit. Four unrelated hit-testing strategies sit
  behind one interface, which is the strongest evidence the seam is real.

### Waits live in exactly one place

`Waits.WaitForCondition(...)` is the only *timing* loop in the codebase. Waiting is a
driver-level verb, so every layer above is declarative: a screen writes
`await Driver.WaitForObject(ClaimButton)`, never a retry loop. There is no
`Thread.Sleep` anywhere in the package.

`Waits` must never use `ConfigureAwait(false)` — see the note in the source, and the
bug it records under [What's actually verified](#whats-actually-verified).

---

## The keyword layer — the point of the project

A manual QA engineer should be able to build a test case out of named actions in a
browser, save it, and have it appear as a real Unity test in CI — without opening
Unity, without writing C#, and without waiting for a compile.

The mechanism, in four parts:

**1. Keywords are opt-in, via attribute.**

```csharp
[ScenarioAction("UseItemOn",
    Description = "Selects a carried item and uses it on something in the room.")]
public async Task UseItemOn(string item, string target)
{
    if (!await _screen.IsCarrying(item))
    {
        throw new InvalidOperationException($"Cannot use '{item}' — it is not being carried.");
    }

    await _screen.SelectInventoryItem(item);
    await _screen.ClickHotspot(target);
}
```

An attribute rather than raw reflection over method names, so the keyword string
survives a C# rename and private helpers never leak into the vocabulary.

**2. Keywords are intent-level, not primitive.** `PlayUntilLevelUpReady`, `UnlockGate` —
never `Tap` or `Drag`. See the honest limitation below.

**3. A catalog is exported.** `TestGoblin > Export Keyword Catalog` (or the same method
headless via `-executeMethod`) writes `keywords.json` listing every keyword with its
parameters and types, so the browser UI can render the right input for each argument.
Test assemblies are excluded, so fixtures never reach QA's vocabulary.

The catalog carries the scenes in Build Settings too, for the same reason it carries
keywords: only Unity knows the list, and a scenario naming a scene that is not in it cannot
run. Both are dropdowns in the builder rather than free text, so the two facts an author has
to get exactly right are the two they cannot mistype.

**4. Scenarios are data, never generated code.**

```json
{
  "name": "Player signs in, finds the key and opens the door",
  "scene": "PointAndClick",
  "inputBackend": "Physics2D",
  "steps": [
    { "action": "SignIn",         "arguments": { "maxLoadingSeconds": 3.0 } },
    { "action": "PickUp",         "arguments": { "item": "Key" } },
    { "action": "UseItemOn",      "arguments": { "item": "Key", "target": "Door" } },
    { "action": "AssertDoorState","arguments": { "expected": "Open" } }
  ]
}
```

Scenario files live in `<projectRoot>/TestGoblin/Scenarios~/` and are named
`*.scenario.json`. A single `[ValueSource]` fixture enumerates that folder and yields
**one NUnit test case per file**, so each scenario passes or fails under its own name
in Test Runner and in CI. No `.cs` generation, no domain reload, no compile step
between "QA hits save" and "the test runs".

`ScenarioExecutor` binds every step against the registry *before* running any of them,
so a typo in the last step costs no game state — and its error messages name the
scenario, the step number, the action and the file path, because the person reading
them cannot read the C#.

### The honest limitation

Keyword-driven testing has a well-known failure mode: the vocabulary is either too
small to express real test cases, or it grows conditionals, loops and variables until
the JSON is a bad programming language and everyone would have been better off writing
C#. Keeping keywords at intent level is the defence, and it has a real cost —
**extending the vocabulary requires an engineer**. TestGoblin makes QA independent for
*composition*, not for *capability*.

---

## Drivers

| Driver | Hit-tests with | Use when |
|---|---|---|
| `InProcessGameDriver` | `EventSystem.RaycastAll` + `ExecuteEvents` | The game's interactive elements are uGUI widgets |
| `PhysicsGameDriver` | `Physics2D.GetRayIntersectionAll` | Clickable things are sprites with `Collider2D` |
| `Physics3DGameDriver` | `Physics.RaycastAll` | Clickable things are 3D meshes with colliders |
| `UiToolkitGameDriver` | `panel.Pick()` + pointer events | The UI is a `UIDocument` of `VisualElement`s |
| `CompositeGameDriver` | delegates, topmost layer first | A HUD drawn over a world — e.g. UI Toolkit over 3D |
| `FakeGameDriver` | nothing; scripted in memory | Edit-mode tests, and testing your own Screens with no game |
| `AltTesterGameDriver` | out of process, over TCP | Example only — see [licensing](#tool-choice-and-licensing) |

The four in-process drivers share `PointerGameDriverBase`, which owns everything that
is *not* hit-testing: precondition checks, error messages, and yielding a frame after
every tap and drag. Each subclass supplies four members — where a target is on screen,
what is topmost at a point, and two hint strings for failures.

**Taps are refused, not faked.** A tap fails if the target is disabled, off screen, or
covered by something else — rather than invoking the handler directly. That is the
difference between testing the game and testing the game *with* the game. The 3D
sample exists specifically to demonstrate it: the vault's own rules permit taking the
key at any time, and only geometry prevents it, so the refusal can come from nowhere
but the driver's raycast.

**Layering is real occlusion, not ownership.** `CompositeGameDriver` asks every layer
*above* the one owning an object whether it covers that point, so an open inventory
panel makes the 3D gate behind it unclickable. Neither driver could tell on its own.
Cross-layer dragging is refused rather than faked — a drag cannot begin in one input
system and end in another.

Two things the physics drivers do that are easy to get wrong: they call
`Physics.SyncTransforms()` before raycasting (`autoSyncTransforms` is off by default,
so anything moved since the last simulation step is still at its old position as far as
a raycast is concerned), and the 2D driver orders hits by **sorting layer** before
distance, because 2D scenes routinely stack sprites at identical depth.

---

## Configuration

One typed `TestRunConfig`, loaded from JSON with per-field environment-variable
overrides (`TESTGOBLIN_<FIELD>`). Switching targets changes config, never code.

Two independent axes:

| | Values | Means |
|---|---|---|
| `ExecutionTarget` | `Fake`, `Editor`, `StandalonePlayer`, `Device` | *Where* the game runs |
| `InputBackend` | `UGui`, `Physics2D`, `Physics3D`, `UiToolkit` | *How* a click reaches it |

`GameDriverFactory` is the single place that maps config to driver. `CreateLayered`
takes an ordered list of backends and composes them, which is what lets a scenario ask
for `"inputBackend": "UiToolkit,Physics3D"`.

Lookup order for the config file: `TESTGOBLIN_CONFIG_PATH` if set, else
`Packages/com.routaz.testgoblin/config.local.json`. Scenario files can be relocated with
`TESTGOBLIN_SCENARIOS_PATH`.

If no config is present at all, the scenario runner falls back to in-process uGUI — so
a consumer who has just imported the package can run a scenario without first authoring
configuration.

---

## Quick start

**Requirements:** Unity 6 (developed against 6000.3.10f1), .NET 9 SDK for the control
server.

Clone and open `TestGoblin-Dev` in Unity, then:

1. `TestGoblin > Import All Samples` — copies the three samples into `Assets/Samples`
2. Let Unity compile, then generate each sample's scene from the `DemoGame` menu
   (`Rebuild Merge Game Scene`, `Rebuild Point And Click Scene`, `Rebuild Vault (3D) Scene`)
3. `TestGoblin > Install Sample Scenarios` — copies sample scenarios into `TestGoblin/Scenarios~`
4. `Window > General > Test Runner` — run EditMode and PlayMode

Or run the whole thing headless from a clean state:

```bash
UNITY="D:/Editors/6000.3.10f1/Editor/Unity.exe" ci/run-suite.sh
```

That script owns the order the steps must happen in, including two learned the hard
way: clear previously imported samples *first* (a broken sample bricks the very
importer that would fix it, because Unity refuses `-executeMethod` while scripts fail),
and build each sample's scene in its own Unity invocation (the sample's Editor assembly
does not exist until Unity has compiled the freshly imported code).

> Unity locks a project while the editor has it open. **Close the editor before any
> batch-mode run.** The script and the server both check for the lockfile and fail fast.

---

## Samples

Three samples, one per input technology, so you import the one that matches the game
you are making. Each carries its own game, screens, flows, tests and a scenario.

| Sample | Game | Driver | What it demonstrates |
|---|---|---|---|
| **Merge Game (uGUI)** | Login → fake load → 4×4 merge board with an energy time-gate | `InProcessGameDriver` | Drag-to-merge through simulated input; a real time-taking transition; conditional buttons |
| **Point and Click (2D sprites)** | Sign in → find a key → open a door | `PhysicsGameDriver` | Sprite hit-testing, sorting-layer order, an inventory with a cursor-following verb line |
| **Point and Click (3D meshes)** | A vault where the key sits behind a wall, with a UI Toolkit HUD | `Physics3DGameDriver` + `UiToolkitGameDriver` via `CompositeGameDriver` | Occlusion refusal; a mixed-technology game driven as one stack |

The samples live in `Samples~/`, which Unity does not compile — which is exactly how the
AltTester driver rotted into an unverified state. `SampleImporter` uses the same
`Sample.Import()` the Package Manager window calls, so CI compiles and runs them
through the real consumer path rather than a shortcut.

The merge sample's login flow matters more than it looks: its login → loading → game
transition takes 1.5 seconds, and `LoginFlowTests` asserts the wait *actually waited*.
Every other state change in the project is same-frame, so a driver that never waited at
all would have passed everything else.

---

## The control server

A small ASP.NET Core minimal-API app in [`tools/TestGoblin.Server/`](tools/TestGoblin.Server/) —
deliberately outside the Unity project, because anything under `Assets/` gets compiled
by Unity.

Double-click [`tools/testgoblin.cmd`](tools/testgoblin.cmd), or:

```bash
dotnet run --project tools/TestGoblin.Server/TestGoblin.Server.csproj
```

It opens http://127.0.0.1:5111, and serves two pages: a run launcher with parsed
results, and a scenario builder that reads `keywords.json` and writes scenario files.

| Endpoint | Purpose |
|---|---|
| `POST /api/runs?platform=&scenario=` | Launch a batch-mode run; `scenario` narrows it to one file via `-testFilter` |
| `GET /api/runs`, `GET /api/runs/{id}` | Run history and a single run's parsed result |
| `GET /api/keywords` | The exported catalog, for rendering the builder |
| `GET /api/scenarios`, `GET`/`PUT`/`DELETE /api/scenarios/{file}` | List, read, save and delete scenario files |
| `GET /api/live` | Held SSE connection — the liveness signal |

Three design points worth knowing:

- **It binds to loopback only.** It launches processes on your machine and must not be
  reachable from the network. There is no authentication, by design.
- **It exits by itself a few seconds after you close the tab.** Liveness is a held
  server-sent-events connection rather than a heartbeat timer, because browsers throttle
  timers in hidden tabs and a backgrounded-but-open tab would otherwise look closed.
  Closing the tab *during* a run lets the run finish rather than orphaning a Unity process.
- **Results are Unity's own NUnit3 XML**, parsed into TestGoblin's JSON. The same
  artifact CI already consumes — presenting an existing format rather than inventing one.

`ScenarioStore` validates file names against a whitelist and then re-checks that the
resolved path is still inside the scenarios folder, because a tool that writes files at
a name the browser supplied is a path-traversal bug waiting to happen.

Configuration (Unity's path, the project path, shutdown timings) lives in
[`appsettings.json`](tools/TestGoblin.Server/appsettings.json). Run history is held in
memory and lost on restart.

---

## Adopting TestGoblin in your own game

> Never actually done — see the last row of the verification table. This is the intended
> shape, not a walkthrough anyone has followed.

1. Add the package (git URL with `?path=TestGoblin-Dev/Packages/com.routaz.testgoblin`).
2. Add `"com.routaz.testgoblin"` to `"testables"` in `Packages/manifest.json`, or the
   scenario runner will not be discovered.
3. Write your **Screens** — one per screen of your game, each owning its locators as
   `private const` strings, deriving from `ScreenBase`.
4. Write your **Flows** — intent-level methods over those screens, each marked
   `[ScenarioAction("Name")]`. This is your QA vocabulary; choose the names carefully,
   because they end up in files QA maintains.
5. Export the catalog, author scenarios into `<projectRoot>/TestGoblin/Scenarios~/`, and
   commit them — they are test cases your team owns, not generated output.
6. Point `TESTGOBLIN_INPUT_BACKEND` at whatever your game's clickable things are, and run.

The package never sees your game. Keywords are found by a scan of loaded assemblies, so
your flows assembly is discovered without TestGoblin knowing it exists — proven here by
the samples, whose assemblies the package has no reference to.

---

## Execution tiers and CI

Two tiers, same suite, differing only by config:

| Tier | Trigger | Target | Cost | Catches |
|---|---|---|---|---|
| **PR smoke** | every pull request | editor / in-process | ~free, no device farm | logic, wiring, wait and timeout regressions, scenario validity |
| **Nightly device** | schedule | real device via provider | licence + device-farm cost | touch quirks, notches, permissions, perf, real backend |

See [`ci/github-actions-example.yml`](ci/github-actions-example.yml).

**Honest caveat:** the cheap tier validates framework logic and flow composition. It does
**not** validate touch-input fidelity, device-specific UI, OS permission dialogs, or
performance. Those only surface on the device tier, which here is a stub.

One caveat is worth naming concretely, because it was demonstrated rather than guessed:
`InProcessGameDriver` dispatches through `ExecuteEvents` and raycasts via `EventSystem`
directly, so **it never touches the input module**. During development it drove the demo
game perfectly while that game's input stack was misconfigured — a real player would
have hit the failure instantly. An in-process driver cannot verify that input is wired
up at all.

---

## What's actually verified

Last full run: **EditMode 88/88, PlayMode 24/24, `ci/run-suite.sh` exit 0.**

| Piece | Status |
|---|---|
| `FakeGameDriver` — waits, timeouts, tap/drag recording, object scripting | ✅ **Proven** — 12 edit-mode tests |
| Config load + env-var overrides + validation | ✅ **Proven** |
| Config → driver mapping (`GameDriverFactory`) | ✅ **Proven** — 14 tests covering every target, backend, layered stack and unmapped case. This is what makes "switching targets changes config, never code" real rather than aspirational |
| Unity headless batch-mode run | ✅ **Proven** — both platforms, exit 0 |
| `InProcessGameDriver` against a live uGUI scene | ✅ **Proven** — logs in, plays a merge game to a level-up and takes it, entirely through simulated input |
| `PhysicsGameDriver` (2D sprites) | ✅ **Proven** — 4 tests, including refusing a sprite covered by another at identical depth |
| `Physics3DGameDriver` (3D meshes) | ✅ **Proven** — 4 tests; the vault scenario is refused while the wall is in the way and succeeds once the lever retracts it |
| `UiToolkitGameDriver` | ✅ **Proven** — 5 tests. Shares no machinery with the pointer drivers and still satisfies `IGameDriver` |
| `CompositeGameDriver` (occlusion across technologies) | ✅ **Proven** — 4 tests; an open UI Toolkit inventory makes a 3D gate unreachable |
| Tap fidelity (interactable, on-screen and reachability preconditions) | ✅ **Proven** — a tap fails rather than invoking the handler directly |
| Polling waits against a real, time-taking transition | ✅ **Proven** — the 1.5s login → loading → game transition, asserted to have actually waited |
| Keyword discovery (`[ScenarioAction]` + `KeywordRegistry`) | ✅ **Proven** — 10 tests, mostly covering what it *refuses*: duplicate names, non-`Task` returns, unrepresentable parameter types |
| `keywords.json` catalog export | ✅ **Proven** — runs headless via `-executeMethod`; excludes test assemblies |
| Scenario format + `ScenarioExecutor` | ✅ **Proven** — 11 tests, most asserting on *error message content*, because the person reading them cannot read the C# |
| Scenario → NUnit test case | ✅ **Proven** — three sample scenarios run headless, each as its own named case, with no generated code |
| Scenario builder UI | ✅ **Proven** — a scenario authored in the browser was saved to disk and ran as its own test |
| Running scenarios from the browser | ✅ **Proven** — suite selector runs EditMode or PlayMode; unknown platforms rejected with 400 |
| Server self-shutdown when the UI closes | ✅ **Proven** — tab-closed, tab-held-in-background, and closed-mid-run (run completes first) |
| UI state recovery on reload | ✅ **Proven** — reloading during a run resumes it; after one, restores results |
| Package contains no game-specific code | ✅ **Proven** — keywords in assemblies the package does not reference are still discovered and still run |
| Samples compile and run through the real import path | ✅ **Proven** — CI imports via `Sample.Import()` and runs their tests |
| Backend seam (`IBackendTestApi`) | ⚠️ Fake proven; a real HTTP implementation is **stubbed** |
| `AltTesterGameDriver` | ⚠️ **Example only — not compiled.** Compiler-verified against SDK 2.3.2 before the SDK was removed; now inert code behind `#if ALTTESTER_PRESENT` that nothing builds, and it can rot without anything noticing |
| Device / standalone execution tiers | ⛔ **Stubbed** — `GameDriverFactory` throws for them unless the AltTester SDK is present, deliberately, so an unavailable tier fails loudly rather than silently substituting a driver that tests something else |
| **Package install into a fresh project via git URL** | ⛔ **Never tested.** The known blockers are fixed — `testables` declared, scenarios moved into the consuming project, package purged of game code — but nothing has been installed anywhere. This is the single largest unproven claim on this page |

### Two bugs worth recording

Both are the kind of defect a fake driver structurally cannot catch, and both were found
only by pointing a real driver at a real scene:

- **A wrong locator.** `GameBoardScreen` looked for an object named `GameBoardScreen`;
  the scene called it `GameBoard`. A `TODO(verify)` that stayed unverified because no
  test had ever touched a real scene.
- **A threading bug.** `Waits` used `ConfigureAwait(false)`, discarding Unity's
  synchronization context, so every poll after the first resumed on a threadpool thread.
  The fake never noticed — it only touches a dictionary. Against a real scene this passed
  whenever an object was already present and threw whenever it had to wait, which reads
  as flakiness rather than as a bug.

A third was found by play-testing rather than by any test: objects positioned outside the
camera's view were still clickable, because "is it hit-testable" and "can the player see
it" are different questions. That is now the `RequireOnScreen` precondition.

---

## Tool choice and licensing

| Tool | Role here | Why / why not |
|---|---|---|
| **Unity Test Framework** | **The execution engine** | Runs the NUnit suite in edit mode, play mode or headless batch mode. Free, already present, produces standard NUnit3 XML that CI understands |
| **In-process drivers** | **The default** | Run inside play mode and drive the live scene directly. No licence, no TCP, no external app — so the whole suite runs free and headless on a plain CI VM |
| AltTester | Worked example, not a dependency | Sees the Unity scene graph from *outside* the process — the right tool for a real build on real hardware, which an in-process driver fundamentally cannot test |
| Appium | Native-shell only (out of scope) | A Unity game renders into a single native GL surface — Appium sees one activity and nothing inside it. Useful for OS dialogs, store flows and permissions; not for in-game objects |

**On AltTester specifically.** Its free SDK is GPL-3, which is unsuitable for a
closed-source commercial game; a paid subscription grants a non-GPL licence, making it
effectively mandatory for a commercial title regardless of budget. Cost scales with seats
plus a parallel-connection add-on, not with the number of titles — for a large nightly
regression suite, the parallelism add-on is the number to watch.

That is precisely why the default driver is in-process. The paid tier buys something real
— testing an actual build on actual hardware — but it should not be on the critical path
for every pull request, because making it the default means no tests run at all until
someone buys a licence.

The SDK has since been removed from this project entirely: an MIT-licensed package that
links GPL-3 assemblies for a tier it cannot exercise is carrying a licensing question it
does not need to answer. `AltTesterGameDriver` survives as an example of how the
out-of-process tier would be written, at the honest cost recorded in the table above.

---

## Assumptions and limitations

- **Unity 6 (6000.0+).** Developed against 6000.3.10f1. Older versions are untested and
  not claimed.
- **Batch-mode runs need the editor closed** — Unity locks the project.
- **Fixtures using any in-process driver must be play mode.** Their waits rely on the
  captured synchronization context marshalling continuations back to Unity's main thread,
  and that needs a pumping frame loop. Edit mode has none — which is why the fake-driver
  tier exists.
- **Locators are strings.** Every one is a `private const` inside its Screen, marked
  `// TODO(verify)` where guessed. A UI rename means one line changes, but nothing checks
  them at compile time.
- **The keyword vocabulary needs an engineer to extend.** Composition is QA's; capability
  is not.
- **The backend seam is a stub.** `IBackendTestApi` describes pinning A/B variants,
  seeding player state and reading inventory. Only the in-memory fake exists. Pinning
  variants at setup is what separates deterministic tests from flaky ones, which is the
  point the interface exists to make.
- **The control server is a local developer tool** — loopback-bound, unauthenticated,
  in-memory history.
- **Flows that loop on game state must bound themselves.** Nothing in the framework
  enforces it — `Waits` bounds a single wait, not a loop around several. The merge
  sample's `PlayUntilLevelUpReady` takes a `maxRefills` argument for exactly this reason:
  without it, a game that can never reach the threshold hangs until CI times out and
  reports nothing useful.
- **The 2D sample has no README** while the other two do. Small, but it is the kind of
  gap that makes a sample harder to adopt than it should be.

---

## Repo layout

```
/
├── README.md                       # this file
├── ci/
│   ├── run-suite.sh                # full clean-state suite (this repo's own harness)
│   └── github-actions-example.yml  # two-tier pipeline snippet
├── docs/
│   ├── ai-usage-notes.md
│   └── roadmap.md
├── tools/                          # NOT part of the Unity project — plain .NET
│   ├── testgoblin.cmd
│   └── TestGoblin.Server/          # local control server + browser UI
└── TestGoblin-Dev/                 # Unity 6 host project
    ├── Assets/Tests/               # TestGoblin's OWN tests — not shipped
    ├── TestGoblin/Scenarios~/      # scenario files (generated here; authored in a real project)
    └── Packages/com.routaz.testgoblin/
        ├── Runtime/                # Driver, Config, Scenarios, Screens, Backend
        ├── Editor/                 # catalog export, sample import, scenario menu
        ├── Tests/PlayMode/         # ScenarioRunnerTests — the only test that ships
        └── Samples~/               # three importable samples
```

Two placement rules the layout enforces rather than describes:

- **Nothing game-specific enters the package.** Screens and Flows modelling a particular
  game are per-title code. The package ships `ScreenBase` and the driver and scenario
  machinery; it never ships a screen that knows a button's name.
- **TestGoblin's own tests live in the host project**, where they exercise the package's
  public surface exactly as a consumer would. Adding the package to `testables` gets you
  the scenario runner and nothing else.

---

## Roadmap

1. **Install the package into a fresh project via git URL.** The anticipated blockers are
   fixed, but the install has never been performed — expect it to surface more.
2. A real HTTP-backed `IBackendTestApi`.
3. `[Category]` tagging so tiers can run different subsets.
4. A licensed AltTester device run, which would put the out-of-process tier back under a
   compiler.

See [`docs/roadmap.md`](docs/roadmap.md) for the fuller list.

---

## Licence

MIT.
