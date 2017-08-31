#!/bin/bash

function install_build_deps(){
  if [ ! -d $HOME/dotnet/2.0 ]; then
    ss apt update && ss apt install libunwind8 libunwind8-dev gettext libicu-dev liblttng-ust-dev libcurl4-openssl-dev libssl-dev uuid-dev unzip -y
    mkdir -p $HOME/dotnet/2.0
    curl -o $HOME/dotnet/2.0/dotnet.tar.gz 'https://download.microsoft.com/download/1/B/4/1B4DE605-8378-47A5-B01B-2C79D6C55519/dotnet-sdk-2.0.0-linux-x64.tar.gz'
    tar -xvf $HOME/dotnet/2.0/dotnet.tar.gz -C $HOME/dotnet/2.0
  fi
  export DOTNET=$HOME/dotnet/2.0/dotnet
}

function ss(){
  if [ ! -f /usr/bin/sudo ]; then
    echo $(exec $@)
  else
    echo $(exec sudo $@)
  fi
}

function build() {
  install_build_deps
  $DOTNET build $@
}

cd $(dirname $0)

build -c Debug
build -c Release