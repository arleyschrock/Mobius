#!/bin/bash

function ss(){
  if [ ! -f /usr/bin/sudo ]; then
    echo $(exec $@)
  else
    echo $(exec sudo $@)
  fi
}

function install_build_deps (){
  if [ ! -d $FWDIR/dependencies/dotnet/2.0 ]; then
    ss apt update && ss apt install libunwind8 libunwind8-dev gettext libicu-dev liblttng-ust-dev libcurl4-openssl-dev libssl-dev uuid-dev unzip -y
    mkdir -p $FWDIR/dependencies/dotnet/2.0
    curl -o $FWDIR/dependencies/dotnet/2.0/dotnet.tar.gz 'https://download.microsoft.com/download/1/B/4/1B4DE605-8378-47A5-B01B-2C79D6C55519/dotnet-sdk-2.0.0-linux-x64.tar.gz'
    tar -xvf $FWDIR/dependencies/dotnet/2.0/dotnet.tar.gz -C $FWDIR/dependencies/dotnet/2.0
  fi
  export DOTNET=$FWDIR/dependencies/dotnet/2.0/dotnet
}

function dotnet_build() {
  install_build_deps
  eval $DOTNET build $@
  PUBLISH=
  for arg in $@
  do
    if [ "$arg" == "Release" ];
    then 
      eval $DOTNET publish -c Release
    fi
  done
  
}

function project_build(){
  DEPLOY=$(dirname $0)
  if [ "$DEPLOY" == "." ];
  then
    export DEPLOY=$PWD
  fi

  echo $DEPLOY
  ls $DEPLOY
  
  export DEPLOY=$DEPLOY/$(echo )
  dotnet_build -c Debug 
  dotnet_build -c Release 
}

function show_usage(){
  echo "Usage: dotnet-build.sh <project directory>"
  exit 1
}
if [ "$1" != "" ]; 
then
  if [ -d $1 ]; 
  then  
    cd $1
    export PROJECT=$PWD | rev | cut -d'/' -f1 | rev
    echo PROJECT: $PROJECT
    project_build
  else 
    show_usage
  fi
else 
  show_usage
fi