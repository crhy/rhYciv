#!/usr/bin/env bash
# Build libraylib.so for Android arm64-v8a from the raylib 5.0 source, matching
# Raylib-CSharp 5.0.0 (docs/MOBILE-PORT.md). Requires ANDROID_HOME with an NDK.
# Output: Android/native/libraylib.so
set -euo pipefail

NDK="${ANDROID_HOME:?ANDROID_HOME must point at the Android SDK}/ndk"
NDK="$(ls -1 "$NDK" | sort -V | tail -1)"
NDK="$ANDROID_HOME/ndk/$NDK"

HERE="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$HERE/.raylib-src"

if [ ! -d "$SRC" ]; then
    git clone --depth 1 --branch 5.0 https://github.com/raysan5/raylib "$SRC"
fi

# NDK r28 marks ALooper_pollAll as unavailable; raylib 5.0's Android backend
# must use ALooper_pollOnce instead.
sed -i 's/ALooper_pollAll/ALooper_pollOnce/' "$SRC/src/platforms/rcore_android.c"

mkdir -p "$HERE/Android/native"
"$NDK/toolchains/llvm/prebuilt/linux-x86_64/bin/clang" \
    --target=aarch64-none-linux-android21 \
    -DPLATFORM_ANDROID -DGRAPHICS_API_OPENGL_ES2 -DUSE_AUDIO \
    -DSUPPORT_FILEFORMAT_JPG -DSUPPORT_FILEFORMAT_PNG \
    -O2 -fPIC -I"$SRC/src" \
    "$SRC/src/rcore.c" "$SRC/src/rshapes.c" "$SRC/src/rtext.c" \
    "$SRC/src/rtextures.c" "$SRC/src/rmodels.c" "$SRC/src/utils.c" \
    "$SRC/src/raudio.c" \
    -o "$HERE/Android/native/libraylib.so" \
    -shared -Wl,-soname,libraylib.so \
    -lEGL -lGLESv2 -lOpenSLES -lm -llog -landroid

echo "built $HERE/Android/native/libraylib.so"
