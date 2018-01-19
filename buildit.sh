#!/bin/sh

nant /clean
echo "Scrubbing Directories"
find . -name "*.csproj" -type f -print0 | xargs -0 /bin/rm -f
find . -name "*.csproj.user" -type f -print0 | xargs -0 /bin/rm -f
find . -name "*.build" -type f -print0 | xargs -0 /bin/rm -f
find . -name "*Temporary*" -type f -print0 | xargs -0 /bin/rm -f
find . -name "*.cache" -type f -print0 | xargs -0 /bin/rm -f
find . -name "*.rej" -type f -print0 | xargs -0 /bin/rm -f
find . -name "*.orig" -type f -print0 | xargs -0 /bin/rm -f
find . -name "*.pdb" -type f -print0 | xargs -0 /bin/rm -f
find . -name "*.mdb" -type f -print0 | xargs -0 /bin/rm -f
echo "Running Prebuild"
./runprebuild.sh
echo "Building Release, MS Debug Info sucks on linux"
msbuild /p:Configuration=Release /p:AutoGenerateBindingRedirects=true
echo "Build Done, check for errors"
