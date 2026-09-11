#!/bin/sh
export DOTNET_ROOT=/app/lib/dotnet
cd /app/lib/rhyciv || exit 1

# Keep whatever the process writes to its own output streams.
#
# The game's own crash handlers cover managed exceptions, and the session record
# covers a fault that kills the process before any handler runs -- but neither
# sees what the runtime itself prints on the way down. A stack overflow, a failed
# assertion in a native library and a graphics driver abort all report themselves
# on standard error and then take the process with them, and when the game is
# started from a desktop icon there is no terminal for any of it to land in. That
# is the difference between "it crashed on turn 49" and knowing why.
#
# Kept beside the saves and the crash reports, for the same reason: the
# application directory is read-only in a Flatpak installation.
log_dir="${XDG_DATA_HOME:-$HOME/.local/share}/rhYciv/Logs"
if mkdir -p "$log_dir" 2>/dev/null; then
    # The run that crashed wrote its output here, and this launch is about to
    # overwrite it -- so it is kept aside first. The crash report the game writes
    # a moment from now folds it in; appending to one ever-growing file instead
    # would cost every player disk for the benefit of the few who crash.
    [ -f "$log_dir/session-output.log" ] &&
        mv -f "$log_dir/session-output.log" "$log_dir/session-output.previous.log"
    exec /app/lib/dotnet/dotnet RaylibUI.dll "$@" >"$log_dir/session-output.log" 2>&1
fi

exec /app/lib/dotnet/dotnet RaylibUI.dll "$@"
