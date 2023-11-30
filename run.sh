#!/bin/sh

# params
# 1: no value to run once and quit (polling mode)
# 1: screen to run listener on screen -- check and start if necessary

d=$(realpath "$0")
wd=$(dirname "$d")

if [ ! -f "${wd}/appsettings.linux.json" ]; then
	echo "appsettings.linux.json does not exist at ${wd}/appsettings.linux.json"
	exit 1
fi

cd "$wd"

if [ -z "$1" ]; then
	dotnet run --launch-profile "ImapCommandReceiver (linux)" --project "${wd}/Rwb.ImapCommandReceiver.csproj"
elif [ "$1" = "screen" ]; then
	if [ $( screen -ls | grep ImapCommandReceiver | wc -l ) -eq 0 ]; then
		# need to start
		logger -s "Starting ImapCommandReceiver on screen."
		/usr/bin/screen -dm -S ImapCommandReceiver -Logfile /root/screen.ImapCommandReceiver.log "dotnet run --launch-profile \"ImapCommandReceiver (linux, listen)\" --project \"${wd}/Rwb.ImapCommandReceiver.csproj\""
	fi
fi
