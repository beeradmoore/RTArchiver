#!/usr/bin/env bash

set -e

docker build \
    --tag rt-archiver \
    --file Dockerfile .
