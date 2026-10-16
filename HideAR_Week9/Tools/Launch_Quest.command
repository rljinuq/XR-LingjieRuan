#!/bin/zsh

# Mac 便利入口：只启动已安装的课堂版，不安装 APK，也不启动旧版。
# Mac shortcut: launch only the installed classroom app; do not install or launch the old app.
ADB="/Applications/Unity/Hub/Editor/6000.3.21f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
"$ADB" reconnect offline
if [[ "$("$ADB" get-state 2>/dev/null)" == "device" ]]; then
    "$ADB" shell am start -n com.lingjieruan.hidear.classroom/com.unity3d.player.UnityPlayerGameActivity
else
    print "Connect one Quest by USB and allow USB debugging inside the headset."
    "$ADB" devices
fi
read -r "?Press Enter to close. "
