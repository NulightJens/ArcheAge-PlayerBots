#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 || $# -gt 2 ]]; then
  echo "Usage: $0 /path/to/AAEmu [--check-only]" >&2
  exit 2
fi

aaemu_root="$(cd "$1" && pwd)"
check_only="${2:-}"
module_root="$(cd "$(dirname "$0")/.." && pwd)"
expected_module_root="$aaemu_root/modules/archeage-playerbots"
supported_base="62e3eb1d87da01194802ac886cd500134facad28"
patch_path="$module_root/compatibility/aaemu-1.2-r208022.patch"
sql_source="$module_root/sql/2026-08-25_aaemu_game_bot_archetype_plans.sql"
sql_destination="$aaemu_root/SQL/updates/2026-08-25_aaemu_game_bot_archetype_plans.sql"

if [[ "$module_root" != "$expected_module_root" ]]; then
  echo "Clone this repository at '$expected_module_root' before running the installer." >&2
  exit 1
fi

for required in AAEmu.Game/AAEmu.Game.csproj AAEmu.UnitTests/AAEmu.UnitTests.csproj .git; do
  [[ -e "$aaemu_root/$required" ]] || { echo "Incomplete AAEmu checkout: missing $required" >&2; exit 1; }
done

git -C "$aaemu_root" cat-file -e "$supported_base^{commit}" 2>/dev/null || {
  echo "The supported AAEmu base commit is not present. Fetch AAEmu history and retry." >&2
  exit 1
}
git -C "$aaemu_root" merge-base --is-ancestor "$supported_base" HEAD || {
  echo "This module currently supports AAEmu 1.2 descendants of $supported_base." >&2
  exit 1
}

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

if [[ "$check_only" == "--check-only" ]]; then
  [[ "$already_patched" == true ]] && state=installed || state=ready
  echo "ArcheAge PlayerBots validation passed; state: $state."
  exit 0
elif [[ -n "$check_only" ]]; then
  echo "Unknown option: $check_only" >&2
  exit 2
fi

if [[ "$can_apply" == true ]]; then
  git -C "$aaemu_root" diff --quiet --no-ext-diff || {
    echo "AAEmu has tracked local changes. Commit or move those changes before installation." >&2
    exit 1
  }
  git -C "$aaemu_root" apply "$patch_path"
fi

[[ -e "$sql_destination" ]] || cp "$sql_source" "$sql_destination"
echo "ArcheAge PlayerBots is installed. Rebuild AAEmu and apply the SQL updater before starting the game server."
