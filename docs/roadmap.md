# Roadmap

Planned follow-up work, and concepts documented here rather than implemented yet.
Nothing below is required for the current framework to function — it's green and
usable as-is against the fake driver.

## Run the AltTester path live (pending a Desktop license)

`AltTesterGameDriver` is implemented and compiles against the real AltTester SDK
(2.3.2), behind `#if ALTTESTER_PRESENT`. A uGUI demo scene is built and
instrumented, with objects named to match the Screen locators, so the same
Screen/Flow classes can drive it. The one remaining step is the live connection:
AltTester 2.x routes the driver through AltTester Desktop, which requires a
license. Once a (free-tier) license is available, a play-mode demo test can drive
the scene end-to-end and flip the README's "live scene" row to a demonstrable ✓.

## Real backend test API

Replace the in-memory `FakeBackendTestApi` with an HTTP-backed implementation that
talks to a real staging backend (`config.BackendBaseUrl`) for seeding player
state, reading inventory, and pinning Remote Config / A-B variants. The interface
(`IBackendTestApi`) already defines the seam; only the transport is missing.

## Bot / soak testing

A longer-running "agent" mode that drives the game semi-randomly for extended
periods to surface crashes, memory leaks, and soft-locks that scripted tests miss.
This is a different tool for a different job than deterministic regression tests —
exploratory rather than assertive — and would reuse the same `IGameDriver` seam.

## Distribution options

- **Today:** consumed as an embedded UPM package via git URL with a `?path=` suffix.
- **Future:** publish as a tarball, or to a scoped registry (e.g. a private npm /
  Verdaccio / Unity registry), so consuming projects pin a normal semver version
  instead of a git ref — cleaner upgrades and no dependency on repo layout.

## Smoke/regression tagging

Add `[Category("Smoke")]` (and similar) to tests so the CI tiers can run different
subsets — a fast smoke set on every PR, the full suite nightly — instead of running
everything everywhere.

## Learning note: evaluate BDD / Gherkin

A keyword/scenario layer that lets manual QA compose tests as data (on top of the
same Flow verbs engineers call) is a natural extension. Before committing to a
design, read up on **BDD** and the **Gherkin** given/when/then syntax (Reqnroll —
the maintained successor to the retired SpecFlow — for .NET) and decide whether a
full BDD stack is worth its weight here versus a lighter custom keyword layer.
Noted as something to learn properly first, not a settled plan.
