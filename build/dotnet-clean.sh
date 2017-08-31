#!/bin/bash

#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#
if [ -d $1 ]; then
cd $1
fi
echo $@
for name in obj bin
do
  for d in $(find . -type d -name $name)
  do
    echo "rm -rf '$d'"
    rm -rf $d
  done
done
