set AppName=abc.smapi.gameloader

adb shell am force-stop %AppName%

smapi-androidtool.exe upfiles --files "bin/Debug/net6.0/ExtraMachineConfig.dll" --dest "Mods/ExtraMachineConfig"

adb shell am start -n %AppName%"/crc64e91f1276c636690c.LauncherActivity" --ez "IsClickStartGame" true