#!/bin/bash

set -e

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/edgelet
BUILD_DIR_REL="target/release"
BUILD_DIR="$PROJECT_ROOT/$BUILD_DIR_REL"

CARGO_HOME=${CARGO_HOME:-"$HOME/.cargo/"}
RUSTUP_HOME=${RUSTUP_HOME:-"$HOME/.rustup"}
IMAGE="edgebuilds.azurecr.io/debian-build:8.11-1"

REVISION=${REVISION:-1}
DEFAULT_VERSION=$(cat $PROJECT_ROOT/version.txt)
VERSION="${VERSION:-$DEFAULT_VERSION}"

docker pull "$IMAGE"

run_command()
{
    echo "$1"
    docker \
      run \
      --rm \
      -e "USER=root" \
      -e "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/cargo/bin" \
      -e "RUSTUP_HOME=/rustup" \
      -v "$PROJECT_ROOT/target:/target" \
      -v "$BUILD_REPOSITORY_LOCALPATH:/project" \
      -v "$CARGO_HOME:/cargo" \
      -v "$RUSTUP_HOME:/rustup" \
      -i "$IMAGE" \
      sh -c "$1"
}

mkdir -p $BUILD_DIR

COMMAND="cd /project/edgelet && make deb8 VERSION=${VERSION} DPKGFLAGS=\"-b -us -uc -i\""

run_command "$COMMAND"
