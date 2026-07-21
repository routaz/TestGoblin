# AI Usage Notes

An honest running log of how AI (Claude Code) was used while building TestGoblin.
The short version lives in the README's disclosure section; this is the detail
behind it.

## How AI was used

AI acted mainly as a **Senior Co-worker**, not an autopilot. The pattern for
each component was: discuss *what* we were building and *why* before any code,
sketch interfaces/skeletons, then I implement or review. Where the work was pure
boilerplate or support scaffolding, I sometimes had the AI write a first draft and
then reviewed and modified it.

Being precise about the split, because the disclosure is only worth anything if
it's accurate:

**Written and owned by me (the framework core):**
- `TestRunConfig`, `ConfigLoader` and its env-var/validation logic.
- `IGameDriver`, `GoblinObject`, `Waits`, the `FakeGameDriver` logic, and the
  `AltTesterGameDriver` stub.
- `ScreenBase`, all Screens, and `GameFlows`.
- The `BaseTest` lifecycle bodies, the driver/backend factory switches.
- `IBackendTestApi` (I extended it — e.g. added `Reset()` when a real caller needed it).
- Every architecture and naming decision;

**AI-drafted, then reviewed and modified by me:**
- Some NUnit test bodies (config, fake-driver, FTUE/daily-reward) from a design we
  agreed first.
- Support/boilerplate classes (`PlayerProfile`, `FakeBackendTestApi`) and small
  pieces like the `ScriptedObject` helper and a backing dictionary.
- The CI workflow YAML.
- The real `AltTesterGameDriver` implementation, once the SDK was installed — AI
  looked up the AltTester 2.3.2 API from the docs and wrote the calls; the
  compiler then verified every one against the installed assembly (I fixed the
  namespace and added the missing driver-assembly reference).
- First drafts of the docs (this README, roadmap, these design notes), which I
  edited for wording, scope, and accuracy.

**AI as reviewer — bugs and issues it caught that I then fixed:**
- `IGameDriver` first drafted with `static` interface members (not overridable) —
  reworked into a proper instance contract.
- `GoblinObject` initially mirrored AltTester's internal object shape — trimmed to
  a minimal framework-owned value type.
- Blocking `.Result` / `.Wait()` in a flow, and `Assert.ThrowsAsync` in tests —
  both deadlock Unity's Test Runner.
- A `MakeFileNameSafe` bug that only kept the last replacement.

## Concepts AI explained (so I could write/defend the code)

- `async`/`await` under Unity's single-threaded context: why the wait loop
  deadlocked the Test Runner, and why `ConfigureAwait(false)` in the driver layer
  fixes it. (This was a real debugging session — two genuine bugs plus a
  stale-binary red herring.)
- The `init` accessor compiling to an `IsExternalInit` modreq, and why Unity 6's
  BCL needs a three-line shim to supply that type.
- Dependency Inversion / the driver seam; the Page Object pattern and locator
  discipline; explicit waits vs. `Thread.Sleep`.
- AltTester vs. Appium for Unity; GPL-3 vs. commercial licensing for an embedded SDK.
- Optional dependencies in UPM: gating the AltTester driver behind an asmdef
  Version Define + `#if ALTTESTER_PRESENT`, so the package compiles with or without
  the SDK. Also: adapting AltTester's synchronous API to the async `IGameDriver`
  contract with `Task.Run`, and why that's safe only because AltTester is
  out-of-process.
- `string.Format` vs. interpolation for const locator templates; BDD/Gherkin
  (flagged as something to learn properly before adopting — see the roadmap).

## Why disclose this

The role I was interviewing/building toward values knowing *how* engineers use AI.
The honest answer is: as a fast senior pair-programmer for review, explanation, and
boilerplate — while keeping ownership of the architecture.
