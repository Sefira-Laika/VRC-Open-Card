A free open source software card game for VRC.

Intended for use in worlds that are not focused only on gaming. Similar to Pool tables in many worlds.

Tested on Linux using ALCOM to manage Unity projects and ADB to receive debug data from mobile test players.

From testing I found that the test PC must have VRC installed. Otherwise unity will freeze permanently every second U# compile.

For android debugging I have been using this command: 
````
adb logcat | grep "Unity"
````
