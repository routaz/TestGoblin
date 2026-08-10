#!/usr/bin/env bash
#
# Runs TestGoblin's full suite from a clean state.
#
# The order here is not arbitrary — each step depends on the one before it, and
# several of the dependencies are non-obvious enough that they were discovered the
# hard way. See the comments on each step.
#
# NOT A TEMPLATE FOR A CONSUMING PROJECT.
#
# Most of this exists because this repository ships samples and has to verify them:
# importing several games into one project, generating a scene per sample, and
# installing several games' scenarios side by side. A project that adopts TestGoblin
# has ONE game, so its version is roughly:
#
#     Unity -batchmode -runTests -testPlatform PlayMode -testResults results.xml
#
# with the driver chosen once via TESTGOBLIN_INPUT_BACKEND. If you are copying this
# file, copy the last step and leave the rest.
#
# Usage:
#   UNITY=/path/to/Unity ci/run-suite.sh
#
# On Windows (Git Bash):
#   UNITY="D:/Editors/6000.3.10f1/Editor/Unity.exe" ci/run-suite.sh

set -uo pipefail

UNITY="${UNITY:-}"
PROJECT="${PROJECT:-$(cd "$(dirname "$0")/.." && pwd)/TestGoblin-Dev}"
RESULTS="${RESULTS:-test-results}"
LOGS="${LOGS:-$RESULTS/logs}"

if [[ -z "$UNITY" ]]; then
  echo "UNITY is not set. Point it at your Unity executable." >&2
  exit 1
fi

mkdir -p "$RESULTS" "$LOGS"

# Runs Unity in batch mode and fails loudly. Every step needs the project to
# compile, so a failure here is almost always a compile error in the log.
unity() {
  local description="$1"; shift
  local log="$LOGS/$(echo "$description" | tr ' ' '-').log"

  echo "--- $description"

  if ! "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$log" "$@"; then
    echo "FAILED: $description (see $log)" >&2
    grep -i "error CS" "$log" | sort -u | head -20 >&2
    return 1
  fi
}

# ---------------------------------------------------------------------------
# 1. Discard previously imported samples.
#
# Samples live in the package's Samples~ folder, which Unity does not compile.
# The copy under Assets/Samples IS compiled — which is the point, since an
# uncompiled sample rots unnoticed. But it also means a broken sample bricks
# recovery: Unity refuses to run ANY -executeMethod while scripts fail to
# compile, including the importer that would replace the broken copy. Starting
# from nothing avoids that deadlock entirely.
# ---------------------------------------------------------------------------
# Refuse outright if the editor has the project open. Unity holds this lock while a
# project is open, and every step below is either a batch-mode run it would block or a
# file deletion under a running editor's feet. Checked BEFORE the delete, because the
# delete is the destructive half — discovering the lock afterwards leaves the project
# missing its samples.
if [[ -f "$PROJECT/Temp/UnityLockfile" ]]; then
  echo "The Unity editor has this project open. Close it before running the suite." >&2
  exit 1
fi

echo "--- Removing previously imported samples"
rm -rf "$PROJECT/Assets/Samples" "$PROJECT/Assets/Samples.meta"

# ---------------------------------------------------------------------------
# 2. Import samples.
#
# Uses the same Sample.Import() the Package Manager window calls, so CI
# exercises the real consumer path rather than a private copy of it.
# ---------------------------------------------------------------------------
unity "Import samples" -quit \
  -executeMethod Routaz.TestGoblin.Editor.SampleImporter.ImportAllFromCommandLine || exit 1

# ---------------------------------------------------------------------------
# 3. Build each sample's scene.
#
# Separate Unity invocations, necessarily: the sample's Editor assembly does not
# exist until Unity has compiled the freshly imported code, and compilation
# happens between sessions, not within one -executeMethod call. Scenes are
# generated rather than shipped so they land with valid references and get
# registered in Build Settings, which SceneManager.LoadScene requires.
#
# These method names are sample-specific. A project adopting TestGoblin would
# list its own scene setup here, or none at all if its scenes are authored.
# ---------------------------------------------------------------------------
unity "Build merge scene" -quit \
  -executeMethod DemoGame.Editor.MergeGameSceneBuilder.BuildFromCommandLine || exit 1

unity "Build point-and-click scene" -quit \
  -executeMethod DemoGame.Adventure.Editor.AdventureSceneBuilder.BuildFromCommandLine || exit 1

unity "Build vault scene" -quit \
  -executeMethod DemoGame.Vault.Editor.VaultSceneBuilder.BuildFromCommandLine || exit 1

# ---------------------------------------------------------------------------
# 4. Install the samples' scenarios.
#
# Scenarios belong to the project, not the package, so they are copied out of
# Assets/Samples into <project>/TestGoblin/Scenarios~ where the runner looks.
# Existing files are left untouched.
# ---------------------------------------------------------------------------
unity "Install sample scenarios" -quit \
  -executeMethod Routaz.TestGoblin.Editor.ScenarioToolsMenu.InstallSampleScenarios || exit 1

# ---------------------------------------------------------------------------
# 5. Export the keyword catalog.
#
# Not needed by the tests — it is what the control server's browser UI reads.
# Exported here so a CI artifact always reflects the vocabulary of this commit.
# ---------------------------------------------------------------------------
unity "Export keyword catalog" -quit \
  -executeMethod Routaz.TestGoblin.Editor.KeywordCatalogExporter.ExportFromCommandLine \
  -keywordCatalogPath "$RESULTS/keywords.json" || exit 1

# ---------------------------------------------------------------------------
# 6. Tests.
#
# Both platforms run even if the first fails, so one report covers everything.
# Unity exits non-zero when tests fail, which is the signal CI wants.
# ---------------------------------------------------------------------------
edit_status=0
play_status=0

unity "EditMode tests" -runTests -testPlatform EditMode \
  -testResults "$RESULTS/editmode.xml" || edit_status=$?

unity "PlayMode tests" -runTests -testPlatform PlayMode \
  -testResults "$RESULTS/playmode.xml" || play_status=$?

echo
echo "=== Summary ==="
for platform in editmode playmode; do
  result="$RESULTS/$platform.xml"

  if [[ -f "$result" ]]; then
    grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' "$result" | head -1 | sed "s/^/$platform: /"
  else
    echo "$platform: no results written — Unity failed before running tests"
  fi
done

if [[ $edit_status -ne 0 || $play_status -ne 0 ]]; then
  exit 1
fi
