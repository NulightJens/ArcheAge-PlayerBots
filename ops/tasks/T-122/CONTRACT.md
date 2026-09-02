# T-122 contract: autonomous signpost kill-quest completion

Start from the exact committed source base in `TASK.yaml` and preserve T-119's
normal NPC intake behavior. This is source-only work; the retained T-120 live
session and its quest state are outside the task.

Generalize nearby quest-start discovery to exact eligible NPC and doodad
objects. For doodads, validate the current object and its quest function before
calling AAEmu's native `AddQuestFromDoodad` lifecycle. Never fabricate active
state, bypass requirements, or accept by directly changing quest records. Keep
main-story priority and deterministic ranking across both giver types.

Add a production quest lifecycle controller for the initially supported shape:
an active quest whose objective is a native monster-hunt objective. Parse the
authoritative objective/template data, select only living hostile target
templates that satisfy the objective, travel through normal bot movement, use
the existing combat controller, and observe authoritative quest progress after
credit. Never grant credit directly. Handle disappearance, death, recovery,
competition, world changes, unreachable targets, timeouts, and unsupported or
ambiguous objectives by suspending safely and exposing the reason.

When AAEmu marks the quest ready, resolve its authoritative report endpoint:
NPC, doodad/signpost, or report-journal where supported. Travel normally when a
world object is required, revalidate in range, report through the normal quest
lifecycle, and select a deterministic valid reward only from the quest's
offered set. After completion, clear controller-owned targets and resume nearby
quest discovery. No per-quest command may be required for acceptance, progress,
report, reward, or chaining.

Use the live-discovered Desireen Signpost object/template shape only as a named
acceptance fixture; do not hard-code object `20451`, template `1744`, its
coordinates, or any quest ID. Add stable read-only debug state and bounded logs
for discovery, acceptance, objective selection, progress, reporting,
completion, suspension, and rescan. Keep new behavior opt-in and validated.

Place required host access in
`compatibility/aaemu-1.2-r208022-doodad-quest-adapter.patch`; never edit a host
checkout. Focused tests must cover NPC compatibility, doodad acceptance,
requirements/rejection, kill-objective interpretation, correct and incorrect
targets, no fabricated credit, normal travel/combat handoff, ready-state
reporting for every supported endpoint, reward validation, chaining, duplicate
prevention, unsupported objectives, and all fail-closed guards. Run an isolated
AAEmu 1.2 build with zero errors and record exact proof in `HANDOFF.md`.

