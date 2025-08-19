#!/usr/bin/env bash

set -e

docker run \
    --rm \
    --name rt-archiver \
    -it --entrypoint=/bin/sh \
    rt-archiver:latest
