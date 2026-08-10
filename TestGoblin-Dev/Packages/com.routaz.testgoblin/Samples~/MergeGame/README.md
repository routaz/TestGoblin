# Merge Game sample (uGUI)

A complete, working example of TestGoblin driving a uGUI game: a login screen, a fake
1.5-second load, and a 4×4 merge board with energy, experience and conditional buttons.

Import this one if **your game's interactive elements are uGUI widgets** — `Button`,
`Image`, anything the `GraphicRaycaster` can hit. It uses `InProcessGameDriver`.

## After importing

1. **`DemoGame > Rebuild Merge Game Scene`** — creates `Assets/Scenes/MergeGame.unity`
   and registers it in Build Settings. The scene is generated rather than shipped so it
   lands in your project with valid references; everything in it is built by
   `MergeGameView` at runtime anyway.
2. **`TestGoblin > Export Keyword Catalog`** — publishes this sample's keywords so the
   scenario builder can offer them.
3. **`TestGoblin > Install Sample Scenarios`** — copies this sample's scenarios into
   `<project>/TestGoblin/Scenarios~/`, where the runner looks. Existing files are left
   alone, so it will not overwrite anything you have edited.

Press Play to play it by hand, or run the tests.

## What each piece demonstrates

| File | Point |
|---|---|
| `Scripts/MergeBoard.cs` | Game rules with no Unity dependency, so they can be unit-tested directly. The framework verifies the game, so the game's own correctness has to stand on its own. |
| `Scripts/MergeGameView.cs` | UI built in code. Conditional buttons are **deactivated**, not disabled, so "the level-up button should not be there yet" is an assertable claim. |
| `Scripts/MergeCell.cs` | Drag and drop. The merge happens in `OnDrop`, which is what the driver dispatches — so simulated and human input take the same path. |
| `TestSupport/*Screen.cs` | Page objects. Object names live here as `private const`, and nowhere else. |
| `TestSupport/MergeGameFlows.cs` | The keyword vocabulary. Intent-level verbs, every loop bounded, marked `[ScenarioAction]`. |
| `Tests/EditMode` | The game's own rules, plus a guard on the keyword names scenarios depend on. |
| `Tests/PlayMode` | The framework driving the real scene, including a wait that must genuinely wait. |

## Things worth copying into your own game

- **Bound every loop.** `PlayUntilLevelUpReady` takes a `maxRefills` and fails with the
  state it reached. An unbounded keyword becomes a hung CI job that reports nothing.
- **Make keywords assert.** `FillEnergy` fails if the button is not being offered. A
  keyword that only taps will pass against a broken game.
- **Set `raycastTarget = false` on labels.** Otherwise a tap lands on the text rather than
  the button, and the driver's hit-test correctly rejects it.
- **Name objects deliberately.** They are the locators; renaming one breaks every scenario
  that uses it.
