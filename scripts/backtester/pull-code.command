#!/bin/bash

# Derived from this script's own location (repo/scripts/backtester/), so double-clicking the
# file works on any machine regardless of the shell's working directory.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SOLUTION="$REPO_ROOT/MovingAverageV1.sln"

cd "$REPO_ROOT" || exit 1

git pull --all
git reset --hard origin/main

dotnet build "$SOLUTION" -c Release

