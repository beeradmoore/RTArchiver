#!/usr/bin/env bash

set -e

dotnet publish \
    RTArchiver/RTArchiver.csproj \
    --output publish/windows/ \
    --runtime win-x64 \
    --self-contained