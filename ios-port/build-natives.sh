#!/usr/bin/env bash
# YARG iOS port: native pieces for the iPhone build (runs on a macOS runner).
#  - BASS and its add-ons (bass, bass_fx, bassmix, bassopus): iOS dynamic
#    frameworks from un4seen.com, downloaded at build time (free for
#    non-commercial use; not stored in this repository).
#  - yarg_audio.framework, built from Native/YargAudio.
#  - STB2CSharp, compiled by Xcode from source under Assets/Plugins/iOS.
# Output: ios-port/Frameworks/*.framework (embedded by Assets/Editor/IosPort).
# Made by Claude Strife - X.
set -euo pipefail

ROOT=$(cd "$(dirname "$0")/.." && pwd)
WORK=$ROOT/ios-port/.work
OUT=$ROOT/ios-port/Frameworks
mkdir -p "$WORK" "$OUT"

step() { printf '\n==> %s\n' "$*"; }

fetch() { # name url...
	local name=$1; shift
	[ -f "$WORK/$name.zip" ] && return 0
	for url in "$@"; do
		if curl -fsSL --retry 3 -o "$WORK/$name.zip" "$url"; then
			echo "downloaded $name from $url"
			return 0
		fi
	done
	echo "could not download $name" >&2
	return 1
}

step "BASS iOS frameworks"
fetch bass24-ios      https://www.un4seen.com/files/bass24-ios.zip
fetch bassmix24-ios   https://www.un4seen.com/files/bassmix24-ios.zip
fetch bassopus24-ios  https://www.un4seen.com/files/bassopus24-ios.zip
fetch bass_fx24-ios   https://www.un4seen.com/files/z/0/bass_fx24-ios.zip https://www.un4seen.com/files/bass_fx24-ios.zip
for zip in "$WORK"/*.zip; do
	dir=$WORK/$(basename "$zip" .zip)
	rm -rf "$dir"; mkdir -p "$dir"
	unzip -q "$zip" -d "$dir"
done
# Each package holds NAME.xcframework; take its device (ios-arm64) slice.
found=0
while IFS= read -r xc; do
	slice=$(find "$xc" -maxdepth 1 -type d -name 'ios-arm64' | head -1)
	[ -n "$slice" ] || slice=$(find "$xc" -maxdepth 1 -type d -name 'ios-arm64*' ! -name '*simulator*' | head -1)
	fw=$(find "$slice" -maxdepth 1 -name '*.framework' | head -1)
	if [ -z "$fw" ]; then
		echo "no device framework in $xc" >&2
		ls -R "$xc" | head -40 >&2
		exit 1
	fi
	rm -rf "$OUT/$(basename "$fw")"
	cp -R "$fw" "$OUT/"
	echo "framework: $(basename "$fw")"
	found=$((found + 1))
done < <(find "$WORK" -maxdepth 3 -iname '*.xcframework' -type d)
[ "$found" -ge 4 ] || { echo "expected 4 BASS frameworks, found $found" >&2; exit 1; }

step "yarg_audio.framework"
cmake -S "$ROOT/Native/YargAudio" -B "$WORK/yarg_audio" -G Xcode \
	-DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_ARCHITECTURES=arm64 \
	-DCMAKE_OSX_DEPLOYMENT_TARGET=15.0 -DYARG_AUDIO_BUILD_TESTS=OFF \
	-DCMAKE_XCODE_ATTRIBUTE_CODE_SIGNING_ALLOWED=NO
cmake --build "$WORK/yarg_audio" --config Release --target yarg_audio -- -sdk iphoneos
ya=$(find "$WORK/yarg_audio" -type d -name 'yarg_audio.framework' -path '*Release-iphoneos*' | head -1)
[ -n "$ya" ] || { echo "yarg_audio.framework was not built" >&2; exit 1; }
rm -rf "$OUT/yarg_audio.framework"
cp -R "$ya" "$OUT/"

step "STB2CSharp source for Xcode"
STB=$ROOT/Assets/Plugins/iOS/STB2CSharp
mkdir -p "$STB"
cp "$ROOT/YARG.Core/STB2CSharp/STB2CSharp.cpp" "$ROOT/YARG.Core/STB2CSharp/STB2CSharp.h" \
	"$ROOT/YARG.Core/STB2CSharp/stb_image.h" "$STB/"

step "Done"
ls -1 "$OUT"
