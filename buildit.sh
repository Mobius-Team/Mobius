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
find . -name "*.bak" -type f -print0 | xargs -0 /bin/rm -f
echo "Running Prebuild"
./runprebuild.sh

echo "Building Release, MS Debug Info sucks on linux"
msbuild /p:Configuration=Release /verbosity:minimal
BUILDCODE=$?

read -r curver <  bin/.version

if [ $BUILDCODE -gt 0 ]; then
echo -e "\e[41m

The Build of version $curver, encoutered and error.\e[0m

"
exit 1
else
echo -e "
\e[30;48;5;82m
Build of version $curver, appears to have suceeded\e[0m
"
fi
echo "done"


