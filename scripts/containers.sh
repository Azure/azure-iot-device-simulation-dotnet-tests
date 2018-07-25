#!/usr/bin/env bash
# Copyright (c) Microsoft. All rights reserved.
# Note: Windows Bash doesn't support shebang extra params
set -e

APP_HOME="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && cd .. && pwd )/"
cd $APP_HOME

start() {
    ./scripts/storageadapter.sh start
    ./scripts/devicesimulation.sh start
}

stop() {
    ./scripts/storageadapter.sh stop
    ./scripts/devicesimulation.sh stop
}

if [[ "$1" == "start" ]]; then
    start
    exit 0
fi

if [[ "$1" == "stop" ]]; then
    stop
    exit 0
fi