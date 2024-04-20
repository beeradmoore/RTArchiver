#!/usr/bin/env bash

set -e

docker run \
    --rm \
    --name rt-archiver \
    -it --entrypoint=/bin/sh \
    #--volume "$LOCAL_ARCHIVE_PATH":/archive \
    --env RT_ARCHIVER_PATH=/archive \
    rt-archiver:latest
