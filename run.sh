#!/bin/sh

d=$(realpath "$0")
wd=$(dirname "$d")

if [ ! -f "${wd}/Rwb.ImapCommandReceiver/appsettings.linux.json" ]; then
	echo "appsettings.linux.json does not exist at ${wd}/Rwb.ImapCommandReceiver/appsettings.linux.json"
	exit 1
fi

dotnet run --launch-profile "ImapCommandReceiver (linux)" --project "${wd}/Rwb.ImapCommandReceiver/Rwb.ImapCommandReceiver.csproj"