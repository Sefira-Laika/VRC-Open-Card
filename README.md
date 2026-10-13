A free open source software card game for VRC.

Intended for use in worlds that are not focused only on gaming. Similar to pool tables in many worlds.

Check example table prefab for a working deck system example. All table components must be located under a unity Game Object. The parent object name is used for getting the table name used to identify cards in debugging. The Code may break without this. Putting all decks on one table object is untested and not recommended, instead put a new empty object to containing each deck and name them something like "Table1-Game2" if you need to identify cards in debugging, having an exact name copy of another deck containing object is untested, but should work as the cards belonging is tested with unity instance IDs.

Tested on Linux using ALCOM to manage Unity projects and ADB to receive debug data from mobile test players.

From testing I found that the test PC must have VRC installed. Otherwise unity will freeze permanently every second U# compile.

For android debugging I have been using this command: 
````
adb logcat | grep "Unity"
````
