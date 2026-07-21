# Design notes: scaling test automation

Three short essays on the harder problems behind a framework like this — scaling
across many titles, coping with runtime-driven config, and where tests should
actually run. These are the *why* behind several of TestGoblin's design choices.

---

## 1. Scaling automation across a large multi-title portfolio without growing the team linearly

The trap is that "more games" naively means "more test engineers." The way out is
to make the *expensive, shared* parts central and the *cheap, specific* parts local.

**Split the framework from the game model.** TestGoblin's core — driver, waits,
config, backend seam — is a versioned shared package. What changes per game is only
the Screens, Flows, and test data. One infrastructure/QA-platform team owns and
evolves the shared package; each title's QA owns its own thin Screens/Flows layer.
Adding the Nth game doesn't touch the framework — it reuses it.

**Version pinning is the anti-fragility mechanism.** Each title pins a TestGoblin
version and upgrades deliberately. A framework improvement can't silently break 40
games overnight; teams adopt on their own schedule. This is the single most
important thing that keeps a shared framework from becoming a liability at scale.

**Measure the fleet.** Track per-title flakiness, coverage, and run time on a shared
dashboard. Scaling isn't just adding tests — it's spotting the title whose suite has
quietly become slow or flaky before it poisons everyone's confidence in CI.

---

## 2. Handling unknown Firebase config at runtime (Remote Config, A/B variants, feature flags)

The core problem: **when behaviour is decided by remote config, the build no longer
determines what the game does.** A test launches, Firebase hands it a random A/B
bucket or a flag that flipped this morning, and the test's expected outcome is
suddenly a coin toss. That's not a flaky test — it's a non-deterministic one, which
is worse because it looks like a real bug.

TestGoblin's answer is the `IBackendTestApi` seam, used three ways:

**Pin variants at setup.** `PinRemoteConfigVariant(key, variant)` locks the test to
a known variant up front, instead of letting Firebase assign a random one. A test
for "the B variant of the reward screen" pins B and asserts B — deterministically.

**Test variants deliberately, not accidentally.** Rather than hoping to stumble into
each variant, parametrize the test over the variants that matter (e.g. NUnit
`[TestCase]`), so every branch is covered on purpose.

**Snapshot the active config as a triage artifact.** `GetActiveConfigSnapshot()`
captures exactly which flags/variants were live for a run. When a test fails against
real config in a nightly build, the first question is always "what config was this?"
— and the snapshot answers it instead of leaving you guessing.

Two more things worth doing: assert that a pin actually *took* (fail loud if the
backend ignored it), and explicitly test the **fallback path** — what the game does
when Remote Config fails to fetch and defaults kick in, which is a real and often
untested production state.

---

## 3. Where tests should run: device farm vs. self-hosted lab vs. cloud VMs/emulators vs. hybrid

**Recommendation: hybrid, tiered by cost and fidelity** — which is exactly what the
two-tier CI here models.

| Option | Strengths | Weaknesses |
|---|---|---|
| **Cloud VMs / emulators** | Cheapest, fast, massively parallel, no hardware to own | No real touch/GPU/perf fidelity; x86 emulator ≠ ARM device; can't catch notches, thermal throttling, real-device quirks |
| **Cloud device farm** (Firebase Test Lab, BrowserStack…) | Real devices, broad model coverage, no capex, elastic | Per-minute cost adds up; queue latency; limited low-level/root access; debugging is more remote |
| **Self-hosted device lab** | Full control, cheapest per-run at very high volume, physical access for debugging | Real capex + ongoing maintenance (OS updates, cabling, flaky USB, device wrangling); doesn't elastically scale |
| **Hybrid** | Right tool per tier | More moving parts to set up |

**The split I'd run:**

- **Every PR (smoke):** editor + fake driver, or emulators/cloud VMs. Cheap, fast,
  parallel — catches logic, wiring, and wait/timeout regressions. No device farm.
- **Nightly / pre-release:** real devices via a cloud device farm — catches touch
  fidelity, safe areas/notches, OS-permission dialogs, and performance that
  emulators structurally cannot.
- **Self-hosted lab:** justified only at high sustained volume or for specific
  hardware you must own (a particular low-end device, a specific GPU). For most
  studios the maintenance burden outweighs the savings until scale is large.

The reason this is affordable is the framework, not the infrastructure: because the
same suite runs on any tier by changing only `TestRunConfig.ExecutionTarget`, you
spend expensive real-device minutes only where they buy fidelity the cheap tier
can't — and run everything else, constantly, for almost nothing.
