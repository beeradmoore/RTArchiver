#!/usr/bin/env bash

set -e 

dotnet publish \
    RTArchiver/RTArchiver.csproj \
    --output publish/macOS/ \
    --runtime osx-x64 \
    --self-contained