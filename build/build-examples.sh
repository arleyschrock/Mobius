#!/bin/bash
FWDIR=$(dirname $0)
#$FWDIR/dotnet-build.sh ../examples

if [ $? -ne 0 ];
then
	echo "Build Mobius .NET Examples failed, stop building."
	exit 1
else
  for i in $(find $FWDIR/../examples -type d -name publish); 
  do
    echo $i
    PROJ1=$(echo $i | rev | cut -d'/' -f6 | rev)
    PROJ2=$(echo $i | rev | cut -d'/' -f5 | rev)
    
    mkdir -p $FWDIR/examples/$PROJ1/$PROJ2
    cp $i/* $FWDIR/examples/$PROJ1/$PROJ2 -rf
  done
fi
