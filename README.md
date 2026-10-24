# MeliciousFurnitureFixPlusLethalPerformanceCompat

**MeliciousFurnitureFixPlusLethalPerformanceCompat**

A targeted compatibility addon for the buyable ship fire exit from MelanieMelicious Utility Furniture Pack and compatibility with LethalPerformance.


## Problem

When using MelanieMelicious Utility Furniture Pack and LethalPerformance the buyable fire exit furniture is rendered useless/buggy.
Either Clients can't use it correctly, or the fire exit shows **The entrance appears to be blocked**.
MeliciousFurnitureFixPlusLethalPerformanceCompat aims to fix these issues and reenable the fire exit furniture als a valuable tool.


## Features

- Keeps LethalPerformance enabled while the ship fire exit finds its paired interior door directly.
- Registers the ship fire exit with LethalLevelLoader for every landing, without moving the furniture into the moon scene.
- Prevents reusing an old entrance ID when the next moon has a different number of normal fire exits.
- Runs registration and activation on clients as well as the host.
- Supports DawnLib's separate fire-exit generation path, including Habitat and transitions from LLL interiors.
- Preserves the original Both, Entrance-only and Exit-only modes.
- Disables the door in orbit and while stored. A newly purchased door becomes available on the next landing.
- Blocks missing or ambiguous destinations rather than choosing a normal moon fire exit at random.

This addon changes the **ship fire exit**.


## Technical Details

- Developed for Lethal Company v81, Utility Furniture Pack **2.0.5**, LethalLevelLoader **1.7.13**, DawnLib **0.9.26**, and LethalPerformance **1.5.3**
- The addon creates `TranslationLookasideBufferModding.MliciosFurnitureFixPlus.cfg` with one Boolean: `[General] Enabled = true`. Restart after changing it. Direction is still configured in the original furniture mod.


## Installation


Install the **same addon version on the host and every client**, together with the same original Fire Exit Type setting. Set the original furniture config to `Both` to allow both directions.

- Mod manager: install the published package normally once available.
- Manual test: copy `MliciosFurnitureFixPlus.dll` to `BepInEx/plugins/MliciosFurnitureFixPlus/` in the selected profile, or use `Install-Test.cmd` from the source archive.
- Restart the game after installation. Buy/place the fire exit before landing.
- Keep only one copy of the addon DLL. Remove the local test copy before installing the published package.

## Disclaimer

This addon was created with AI assistance. Its own source is provided under the MIT license. Referenced projects retain their own licenses.


## Credits

- MelanieMelicious for Utility Furniture Pack.
- IAmBatby and contributors for LethalLevelLoader.
- DiFFoZ and contributors for LethalPerformance.


## Contact

- Contact me via GitHub if you have any questions or issues with the mod