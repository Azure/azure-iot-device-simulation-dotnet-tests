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

#Script Args
repo=""
tag="testing"
act="start"
dockeraccount="azureiotpcs"

set_up_env() {
    export DOCKER_TAG=$tag
    export DOCKER_ACCOUNT=$dockeraccount
}

tear_down() {
    unset DOCKER_TAG
    unset DOCKER_ACCOUNT
}

while [[ $# -gt 0 ]] ;
do
    opt=$1;
    shift;	
    case $opt in
        -dt|--dockertag) tag=$1; shift;;
        -act|--action) act=$1; shift;;
        -da|--docker-account) dockeraccount=$1; shift;; 
        *) shift;
    esac
done

set_up_env

if [[ "$act" == "start" ]]; then
    start
    exit 0
fi

if [[ "$act" == "stop" ]]; then
    stop
    exit 0
fi

tear_down