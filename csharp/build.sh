#!/bin/bash

#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

export FWDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
export CppDll=NoCpp
export XBUILDOPT=/verbosity:minimal

if [ -z $builduri ];
then
  export builduri=build.sh
fi

export PROJ_NAME=SparkCLR
export PROJ="$FWDIR/$PROJ_NAME.sln"

echo "===== Building $PROJ ====="

function error_exit() {
  if [ -z $STEP ]; 
  then
    export STEP=$CONFIGURATION 
  fi
  echo "===== Build FAILED for $PROJ -- $STEP with error $RC - CANNOT CONTINUE ====="
  exit 1
}


export STEP=Debug
export CONFIGURATION=$STEP

dotnet build -c Debug
# msbuild "/p:Configuration=$CONFIGURATION;AllowUnsafeBlocks=true" $XBUILDOPT $PROJ
export RC=$? && [ $RC -ne 0 ] && error_exit
echo "BUILD ok for $CONFIGURATION $PROJ"

echo "Build Release ============================"
export STEP=Release
export CONFIGURATION=$STEP

dotnet build -c Release
export RC=$? && [ $RC -ne 0 ] && error_exit
echo "BUILD ok for $CONFIGURATION $PROJ"

if [ -f "$PROJ_NAME.nuspec" ];
then
  echo "===== Build NuGet package for $PROJ ====="
  export STEP=NuGet-Pack

  dotnet pack "$PROJ_NAME
  export RC=$? && [ $RC -ne 0 ] && error_exit
  echo "NuGet package ok for $PROJ"
fi

echo "===== Build succeeded for $PROJ ====="
