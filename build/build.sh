#!/bin/bash

#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

cd $(dirname $0)
export FWDIR=$PWD

[ ! -d "$FWDIR/dependencies" ] && mkdir "$FWDIR/dependencies"

download_dependency() {
  LINK=$1
  JAR=$2

  if [ ! -e $JAR ];
  then
    echo "Downloading $JAR"
    wget -q $LINK -O $JAR

    if [ ! -e $JAR ];
    then
      echo "Cannot download external dependency $JAR from $LINK"
      popd
      exit 1
    fi
  fi
}

if [ ! -z SUPPRESS_JVM_BUILD ]; 
then

  echo "Download Mobius external dependencies"
  cd "$FWDIR/dependencies"

  SPARK_CSV_LINK="http://search.maven.org/remotecontent?filepath=com/databricks/spark-csv_2.10/1.4.0/spark-csv_2.10-1.4.0.jar"
  SPARK_CSV_JAR="spark-csv_2.10-1.4.0.jar"
  download_dependency $SPARK_CSV_LINK $SPARK_CSV_JAR

  COMMONS_CSV_LINK="http://search.maven.org/remotecontent?filepath=org/apache/commons/commons-csv/1.4/commons-csv-1.4.jar"
  COMMONS_CSV_JAR="commons-csv-1.4.jar"
  download_dependency $COMMONS_CSV_LINK $COMMONS_CSV_JAR

  SPARK_STREAMING_KAFKA_LINK="http://search.maven.org/remotecontent?filepath=org/apache/spark/spark-streaming-kafka-0-8-assembly_2.11/2.0.0/spark-streaming-kafka-0-8-assembly_2.11-2.0.0.jar"
  SPARK_STREAMING_KAFKA_JAR="spark-streaming-kafka-0-8-assembly_2.11-2.0.0.jar"
  download_dependency $SPARK_STREAMING_KAFKA_LINK $SPARK_STREAMING_KAFKA_JAR

  cd $FWDIR

  export SPARKCLR_HOME="$FWDIR/runtime"
  echo "SPARKCLR_HOME=$SPARKCLR_HOME"

  if [ -d "$SPARKCLR_HOME" ];
  then
    echo "Delete existing $SPARKCLR_HOME ..."
    rm -r -f "$SPARKCLR_HOME"
  fi

  [ ! -d "$SPARKCLR_HOME" ] && mkdir "$SPARKCLR_HOME"
  [ ! -d "$SPARKCLR_HOME/bin" ] && mkdir "$SPARKCLR_HOME/bin"
  [ ! -d "$SPARKCLR_HOME/data" ] && mkdir "$SPARKCLR_HOME/data"
  [ ! -d "$SPARKCLR_HOME/lib" ] && mkdir "$SPARKCLR_HOME/lib"
  [ ! -d "$SPARKCLR_HOME/samples" ] && mkdir "$SPARKCLR_HOME/samples"
  [ ! -d "$SPARKCLR_HOME/scripts" ] && mkdir "$SPARKCLR_HOME/scripts"
  [ ! -d "$SPARKCLR_HOME/dependencies" ] && mkdir "$SPARKCLR_HOME/dependencies"

  echo "Assemble Mobius external dependencies"
  cp $FWDIR/dependencies/* "$SPARKCLR_HOME/dependencies/" -rf
  [ $? -ne 0 ] && exit 1

  echo "Assemble Mobius Scala components"
  pushd "$FWDIR/../scala"

  # clean the target directory first
  mvn clean -q
  [ $? -ne 0 ] && exit 1

  # Note: Shade-plugin helps creates an uber-package to simplify running samples during CI;
  # however, it breaks debug mode in IntellJ. So enable shade-plugin
  # only in build.cmd to create the uber-package.
  # build the package
  mvn package -Puber-jar -q

  if [ $? -ne 0 ];
  then
    echo "Build Mobius Scala components failed, stop building."
    popd
    exit 1
  fi
  echo "Mobius Scala binaries"
  cp target/spark*.jar "$SPARKCLR_HOME/lib/"
  popd

  # Any .jar files under the lib directory will be copied to the staged runtime lib tree.
  if [ -d "$FWDIR/lib" ];
  then
    echo "Copy extra jar library binaries"
    for g in `ls $FWDIR/lib/*.jar`
    do
      echo "$g"
      cp "$g" "$SPARKCLR_HOME/lib/"
    done
  fi
fi


echo "Assemble Mobius C# components"

cd $FWDIR
echo $PWD - C#

if [ ! -z SUPPRESS_CLEAN ];
then
  # clean any possible previous build first
  $FWDIR/dotnet-clean.sh ../csharp
fi

$FWDIR/dotnet-build.sh ../csharp

if [ $? -ne 0 ];
then
	echo "Build Mobius C# components failed, stop building."
	exit 1
fi

echo "Copying Mobius C# binaries"
cp $FWDIR/../csharp/Worker/Microsoft.Spark.CSharp/bin/Release/netcoreapp2.0/* "$SPARKCLR_HOME/bin/" -rf

echo "Mobius C# Samples binaries"
# need to include CSharpWorker.exe.config in samples folder
cp $FWDIR/../csharp/Worker/Microsoft.Spark.CSharp/bin/Release/netcoreapp2.0* "$SPARKCLR_HOME/samples/" -rf
cp $FWDIR/../csharp/Samples/Microsoft.Spark.CSharp/bin/Release/netcoreapp2.0* "$SPARKCLR_HOME/samples/" -rf

echo "Mobius Samples data"
cp $FWDIR/../csharp/Samples/Microsoft.Spark.CSharp/data/* "$SPARKCLR_HOME/data/"

echo "Assemble Mobius examples"

if [ ! -z SUPPRESS_CLEAN ]; 
then  
  $FWDIR/dotnet-clean.sh ../examples
fi

$FWDIR/dotnet-build.sh ../examples

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


echo "Assemble Mobius script components"
cd "$FWDIR/../scripts"
cp *.sh  "$SPARKCLR_HOME/scripts/"

echo "zip run directory"
[ ! -d "$FWDIR/target" ] && mkdir "$FWDIR/target"
cd "$SPARKCLR_HOME"
zip -r "$FWDIR/target/run.zip" ./*
