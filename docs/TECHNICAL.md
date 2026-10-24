# Technical notes

## Observed failure

In the local v81 test log, each landing produced an `InvalidParentException` from `FireExitPatch.PreLevelLoad -> Transform.SetParent -> NetworkObject.OnTransformParentChanged`. The 2.0.5 helper reparents the purchased fire exit's parent under `Environment/Teleports`, which is not a valid network parent.

LethalLevelLoader 1.7.13 collects teleports only from its current moon scene. The furniture's reparenting is its attempt to include the ship door in that collection. The native v81 exit lookup matches only direction and entrance ID; it does not clear an old `exitScript` before searching, and it takes the last match when IDs collide. A ship door omitted from generation can therefore keep an old ID that collides with a normal exit on the next moon.

LethalPerformance 1.5.3's `NetworkBehaviourCaching` searches spawned network behaviours. Doors that are not part of the expected spawned-object hierarchy can be missing from this search. This addon does not disable that optimization globally.

## Patch boundaries

- **0.1.1 regression fix:** Habitat is registered as external content in LLL. `DungeonLoader.PrepareDungeon` skips the LLL fire-exit helper for such content; DawnLib's `DungeonRegistrationHandler.AdjustFireExits` handles it instead. Version 0.1.0 only hooked LLL, so takeoff cleared the ship registration and a following Habitat landing never recreated it. The local report's Experimentation/Tower succeeded and Volition/Habitat remained at the wait collider, matching the missing registration logs.
- A separate prefix/transpiler/postfix handles DawnLib's generation method: start a fresh registration, enumerate active exterior exits from scene roots, append available ship exits after the moon's fire exits, and capture IDs after Dawn's ascending numbering. Dawn's own GlobalProp 1231 count then creates the additional interior exit. This runs locally on host and clients and also handles Dawn following LLL in one generation. Only this generation-time lookup is changed; other LP caches remain installed.
- Prefixes on the furniture's two fire-exit helpers skip their original bodies. Their exact prefix registrations on `RoundManager.LoadNewLevel` and `StartOfRound.ShipHasLeft` are also removed to avoid an already-JIT-compiled/inlined helper. Other furniture patches are retained.
- `DungeonLoader.PatchFireEscapes` starts a new landing registration, clears cached ship/interior state, and captures newly assigned exterior IDs afterwards.
- A postfix on `DungeonLoader.GetEntranceTeleports` prepends available ship doors to the moon-scene list. LLL numbers in reverse order, so existing moon exit IDs are preserved and the ship receives extra IDs. LLL's own GlobalProp 1231 count requests the additional interior door.
- One call in `RoundManager.SetExitIDs` is replaced: when a ship door is registered, enumeration traverses loaded scene roots and components, bypassing the optimized global object finder. Native interior-distance ordering and ID assignment remain intact.
- A postfix validates unique opposite-direction matches. A prefix on `EntranceTeleport.FindExitPoint` resolves only the managed ship pairs directly. Normal moon doors retain their original lookup.
- The same LLL hooks execute on clients. In v81 `FinishGeneratingNewLevelClientRpc -> SetLevelObjectVariables -> waitForMainEntranceTeleportToSpawn -> SetExitIDs` initializes client exit IDs. Activation checks local landing state, without an `IsServer` restriction.
- New ship-door instances are initially disabled through an Awake postfix. Cached interactions are updated every 0.5 seconds; this update does not scan scenes. Takeoff clears the registered pairs.

Native teleport methods, player RPCs, animation, item handling and audio are not replaced. The original furniture's direction mode remains authoritative locally; install matching configs on all players.

## Scope and limits

The source inspection and automated checks concern Utility Furniture Pack 2.0.5, LLL 1.7.13, DawnLib 0.9.26 and installed v81 game methods. The installed Habitat 1.2.0 bundle has a GlobalProp 1231 entry, used for its generated fire exits. A lobby test with actual LP and multiple players is pending. A custom interior must support the loader's additional fire-exit generation. If it supplies no matching door or duplicate IDs, the addon blocks its ship route and logs the mismatch.

Late joining into an already generated level and conflicts with mods that change IDs after normal initialization need separate in-game checks. This is not a general fix for every furniture or interior mod.

## Source references

- [LethalPerformance issue 15](https://github.com/DiFFoZ/LethalPerformance/issues/15) documents the same popup and previously listed the buyable furniture fire exit; that entry is marked as previously fixed. It does not prove the current 2.0.5/v81 bug is fixed.
- [Utility Furniture Pack changelog](https://thunderstore.io/c/lethal-company/p/MelanieMelicious/MelanieMelicious_Utility_Furniture_Pack/changelog/) contains earlier multiplayer/fire-exit changes.
- [LethalLevelLoader source](https://github.com/IAmBatby/LethalLevelLoader)
- [DawnLib source](https://github.com/TeamXiaolan/DawnLib)
- [Habitat source](https://github.com/xntkrnl/Interior)
- [Unity FindObjectsOfType ordering](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Object.FindObjectsOfType.html): the fresh scan retains native InstanceID ordering before the original distance sort.

The implementation ships only original addon code, not decompiled upstream sources or original DLLs.
