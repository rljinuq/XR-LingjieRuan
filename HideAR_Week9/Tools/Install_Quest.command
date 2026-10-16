#!/bin/zsh

# Mac 便利入口：用 Unity 自带的 ADB 更新 Week9 APK，再启动独立课堂包名。
# Mac shortcut: use Unity's ADB to update the Week9 APK, then launch its separate package ID.
ADB="/Applications/Unity/Hub/Editor/6000.3.21f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
PROJECT_ROOT=${0:A:h:h}
APK="$PROJECT_ROOT/../Builds/Week9/HideAR_Classroom.apk"
"$ADB" reconnect offline
if [[ "$("$ADB" get-state 2>/dev/null)" != "device" ]]; then
    print "Connect one Quest by USB and allow USB debugging inside the headset."
    "$ADB" devices
elif [[ ! -f "$APK" ]]; then
    print "Classroom APK is missing. Build it in Unity first."
elif "$ADB" install -r "$APK"; then
    "$ADB" shell am start -n com.lingjieruan.hidear.classroom/com.unity3d.player.UnityPlayerGameActivity
fi
read -r "?Press Enter to close. "
