#!/usr/bin/env bash

set -e

docker build \
    --no-cache=true --platform=linux/amd64 \
    --tag rt-archiver \
    --file Dockerfile .
