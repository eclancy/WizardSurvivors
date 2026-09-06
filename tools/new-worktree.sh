#!/usr/bin/env bash
#
# Create a git worktree for a parallel session, with the Godot import cache seeded.
#
#   tools/new-worktree.sh art/beards           -> ../ws-art-beards, new branch art/beards off main
#   tools/new-worktree.sh fix/hud release       branch off release instead of main
#   tools/new-worktree.sh art/x --no-seed       skip the .godot copy
#   tools/new-worktree.sh art/x --dry-run       print what it would do and stop
#
# One session, one worktree, one branch. Several sessions in a single checkout share the
# working tree, the index AND HEAD, so they overwrite each other's edits and rewrite each
# other's commits - which is not a merge conflict you get told about, it is files silently
# changing under you.
#
# Why seeding .godot is safe and worth it: the cache holds no absolute paths (the .md5 files
# are content hashes, and the 2217 *.import files are tracked in git), so it is portable
# between worktrees of this project. Without it the first scene load in a fresh worktree fails
# with "Unrecognized UID" until a full --import walks ~2000 PNGs, which takes several minutes.

set -euo pipefail

usage() {
	sed -n '3,12p' "$0" | sed 's/^# \{0,1\}//'
	exit "${1:-1}"
}

BRANCH=""
BASE=""
SEED=1
DRY=0

for arg in "$@"; do
	case "$arg" in
		--no-seed) SEED=0 ;;
		--dry-run) DRY=1 ;;
		-h|--help) usage 0 ;;
		-*) echo "unknown option: $arg" >&2; usage ;;
		*)
			if [ -z "$BRANCH" ]; then BRANCH="$arg"
			elif [ -z "$BASE" ]; then BASE="$arg"
			else echo "too many arguments" >&2; usage
			fi
			;;
	esac
done

[ -n "$BRANCH" ] || usage

ROOT="$(git rev-parse --show-toplevel)" || { echo "not inside a git repository" >&2; exit 1; }
BASE="${BASE:-main}"
SLUG="$(echo "$BRANCH" | tr '/' '-')"
DIR="$(dirname "$ROOT")/ws-$SLUG"

if [ -e "$DIR" ]; then
	echo "refusing to touch existing path: $DIR" >&2
	echo "remove it first, or pick another branch name." >&2
	exit 1
fi

# A branch can only be checked out in one worktree at a time. Say so in words rather than
# letting git fail three lines later with its own wording.
EXISTING="$(git worktree list --porcelain | awk -v b="refs/heads/$BRANCH" '$1=="branch" && $2==b {found=1} END {print found+0}')"
if [ "$EXISTING" = "1" ]; then
	echo "branch $BRANCH is already checked out in another worktree:" >&2
	git worktree list >&2
	exit 1
fi

if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
	ADD=(worktree add "$DIR" "$BRANCH")
	WHAT="existing branch $BRANCH"
else
	ADD=(worktree add -b "$BRANCH" "$DIR" "$BASE")
	WHAT="new branch $BRANCH off $BASE"
fi

if [ "$DRY" = "1" ]; then
	echo "would run: git ${ADD[*]}"
	[ "$SEED" = "1" ] && echo "would copy: $ROOT/.godot -> $DIR/.godot"
	exit 0
fi

echo "creating $DIR ($WHAT)"
git "${ADD[@]}"

if [ "$SEED" = "1" ]; then
	if [ -d "$ROOT/.godot" ]; then
		echo "seeding the Godot import cache (this is a large copy, give it a moment)"
		cp -r "$ROOT/.godot" "$DIR/.godot"
	else
		echo "no .godot in $ROOT to seed from — the first run in $DIR will need:" >&2
		echo "  \"\$GODOT_BIN\" --headless --path . --import" >&2
	fi
fi

cat <<EOF

  cd "$DIR"
  dotnet build WizardSurvivors.sln

When you are done, from the main checkout:

  git -C "$ROOT" worktree remove "$DIR"

Two rules that matter more than the worktree does:
  * stage explicit paths, never "git add -A" — other sessions leave work in your tree
  * do not rebase or amend a branch another session might be holding
EOF
