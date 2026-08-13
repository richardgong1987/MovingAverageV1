#!/bin/bash

REPO_ROOT=/Users/chenwanli/cAlgo/Sources/Robots/MovingAverageV1
SOLUTION="$REPO_ROOT/MovingAverageV1.sln"

cd $REPO_ROOT

git pull --all
git reset --hard origin/main

dotnet build "$SOLUTION" -c Release

