#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 /path/to/AAEmu [--track aaemu12|aaemu30] [--allow-experimental] [--check-only]" >&2
  exit 2
fi

aaemu_root="$(cd "$1" && pwd)"
shift
check_only=false
allow_experimental=false
track=auto
while [[ $# -gt 0 ]]; do
  case "$1" in
    --check-only) check_only=true; shift ;;
    --allow-experimental) allow_experimental=true; shift ;;
    --track)
      [[ $# -ge 2 ]] || { echo "--track requires aaemu12 or aaemu30" >&2; exit 2; }
      track="$2"
      shift 2
      ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done
module_root="$(cd "$(dirname "$0")/.." && pwd)"
expected_module_root="$aaemu_root/modules/archeage-playerbots"
base_12="62e3eb1d87da01194802ac886cd500134facad28"
base_30="8c1c943bb2309eefffb9da2aa99a408d0acbb095"
sql_source="$module_root/sql/2026-08-25_aaemu_game_bot_archetype_plans.sql"
sql_destination="$aaemu_root/SQL/updates/2026-08-25_aaemu_game_bot_archetype_plans.sql"

if [[ "$module_root" != "$expected_module_root" ]]; then
  echo "Clone this repository at '$expected_module_root' before running the installer." >&2
  exit 1
fi

for required in AAEmu.Game/AAEmu.Game.csproj AAEmu.UnitTests/AAEmu.UnitTests.csproj .git; do
  [[ -e "$aaemu_root/$required" ]] || { echo "Incomplete AAEmu checkout: missing $required" >&2; exit 1; }
done

is_track() {
  local base="$1"
  git -C "$aaemu_root" cat-file -e "$base^{commit}" 2>/dev/null &&
    git -C "$aaemu_root" merge-base --is-ancestor "$base" HEAD
}

if [[ "$track" == auto ]]; then
  matches=()
  is_track "$base_12" && matches+=(aaemu12)
  is_track "$base_30" && matches+=(aaemu30)
  [[ ${#matches[@]} -eq 1 ]] || {
    echo "Could not identify exactly one supported AAEmu lineage; pass --track aaemu12 or --track aaemu30." >&2
    exit 1
  }
  track="${matches[0]}"
fi

case "$track" in
  aaemu12)
    supported_base="$base_12"
    patch_path="$module_root/compatibility/aaemu-1.2-r208022-v2.patch"
    status=supported
    ;;
  aaemu30)
    supported_base="$base_30"
    patch_path="$module_root/compatibility/aaemu-3.0.4.2-r336598-alpha-v3.patch"
    status=server-start-validated
    ;;
  *) echo "Unknown track '$track'; expected aaemu12 or aaemu30." >&2; exit 2 ;;
esac

is_track "$supported_base" || {
  echo "The checkout is not a descendant of the $track tested base $supported_base." >&2
  exit 1
}

if [[ "$status" != supported && "$allow_experimental" != true ]]; then
  echo "$track is $status but awaits complete matching-client runtime acceptance; pass --allow-experimental only for isolated 3.0 development." >&2
  exit 1
fi

already_patched=false
can_apply=false
git -C "$aaemu_root" apply --reverse --check "$patch_path" 2>/dev/null && already_patched=true
git -C "$aaemu_root" apply --check "$patch_path" 2>/dev/null && can_apply=true
if [[ "$already_patched" != true && "$can_apply" != true ]]; then
  echo "The compatibility patch does not apply cleanly." >&2
  exit 1
fi

if [[ -e "$sql_destination" ]] && ! cmp -s "$sql_source" "$sql_destination"; then
  echo "A different migration already exists at '$sql_destination'." >&2
  exit 1
fi

if [[ "$check_only" == true ]]; then
  [[ "$already_patched" == true ]] && state=installed || state=ready
  echo "ArcheAge PlayerBots validation passed; track: $track; state: $state; status: $status."
  exit 0
fi

if [[ "$can_apply" == true ]]; then
  [[ -z "$(git -C "$aaemu_root" status --porcelain=v1 --untracked-files=no)" ]] || {
    echo "AAEmu has tracked local changes. Commit or move those changes before installation." >&2
    exit 1
  }
  git -C "$aaemu_root" apply "$patch_path"
fi

[[ -e "$sql_destination" ]] || cp "$sql_source" "$sql_destination"
echo "ArcheAge PlayerBots is installed for $track. Rebuild AAEmu and apply the SQL updater before starting the game server."
