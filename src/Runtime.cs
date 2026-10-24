using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MliciosFurnitureFixPlus
{
    internal sealed class DoorPair
    {
        internal EntranceTeleport Ship;
        internal EntranceTeleport Interior;
        internal int Id;
        internal bool Active;
        internal bool Reported;
    }

    internal static class Runtime
    {
        internal static Action<string> Log = delegate { };
        internal static Action<string> Warn = delegate { };
        // These providers also allow tests to exercise the real runtime in an isolated Unity scene.
        internal static Func<bool> Landed = delegate {
            StartOfRound round = StartOfRound.Instance;
            return round != null && round.shipHasLanded && !round.shipIsLeaving && !round.inShipPhase;
        };
        internal static Func<EntranceTeleport, bool> Available = IsAvailable;
        internal static Func<int> Mode = delegate {
            Type config = HarmonyLib.AccessTools.TypeByName("MelanieUtilFurnitureConfig");
            FieldInfo field = config == null ? null : config.GetField("fireExit0Type", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object entry = field == null ? null : field.GetValue(null);
            return entry == null ? 0 : Convert.ToInt32(entry.GetType().GetProperty("Value").GetValue(entry, null));
        };
        private static readonly List<DoorPair> pairs = new List<DoorPair>();
        private static readonly HashSet<EntranceTeleport> managedInteriors = new HashSet<EntranceTeleport>();
        private static readonly FieldInfo gotExit = typeof(EntranceTeleport).GetField("gotExitPoint", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo firstCheck = typeof(EntranceTeleport).GetField("checkedForFirstTime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo noExit = typeof(EntranceTeleport).GetField("exitPointDoesntExist", BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool registered;
        private static bool idsReady;

        internal static bool IsShip(EntranceTeleport door)
        {
            return door != null && door.isEntranceToBuilding && door.gameObject.name == "EntranceTeleportShip0";
        }

        internal static void OnCreated(EntranceTeleport door)
        {
            if (IsShip(door)) SetInteraction(door, false);
        }

        private static bool IsAvailable(EntranceTeleport door)
        {
            if (door == null || !door.gameObject.activeInHierarchy || !door.gameObject.scene.IsValid() || !door.gameObject.scene.isLoaded) return false;
            AutoParentToShip parent = door.GetComponentInParent<AutoParentToShip>();
            if (parent == null) return false; // Do not treat an unrelated identically named door as furniture.
            if (parent.disableObject) return false;
            StartOfRound round = StartOfRound.Instance;
            if (round != null && round.unlockablesList != null && parent.unlockableID >= 0 && parent.unlockableID < round.unlockablesList.unlockables.Count)
                return !round.unlockablesList.unlockables[parent.unlockableID].inStorage;
            return true;
        }

        internal static EntranceTeleport[] FreshScan()
        {
            // Scene roots and component traversal bypass Object.FindObjectsOfType's LP replacement.
            List<EntranceTeleport> result = new List<EntranceTeleport>();
            HashSet<int> scenes = new HashSet<int>();
            for (int i = 0; i < SceneManager.sceneCount; ++i) AddScene(SceneManager.GetSceneAt(i), result, scenes);
            if (StartOfRound.Instance != null) AddScene(StartOfRound.Instance.gameObject.scene, result, scenes);
            // Native FindObjectsOfType sorts by InstanceID; retain its tie order in SetExitIDs' distance sort.
            return result.OrderBy(d => d.GetInstanceID()).ToArray();
        }

        private static void AddScene(Scene scene, List<EntranceTeleport> result, HashSet<int> scenes)
        {
            if (!scene.IsValid() || !scene.isLoaded || !scenes.Add(scene.handle)) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<EntranceTeleport>(false));
        }

        internal static void Prepare()
        {
            Reset();
            foreach (EntranceTeleport door in FreshScan().Where(IsShip).Where(Available).OrderBy(StableKey, StringComparer.Ordinal))
            {
                door.entranceId = 20; // Original prefab ID; LLL assigns a fresh non-colliding ID below.
                ClearExit(door);
                pairs.Add(new DoorPair { Ship = door, Id = -1 });
                SetInteraction(door, false);
            }
        }

        internal static void Register(List<EntranceTeleport> entrances)
        {
            if (entrances == null) return;
            // Register only at generation time, even though the furniture remains in the ship scene.
            if (pairs.Count == 0)
                foreach (EntranceTeleport door in FreshScan().Where(IsShip).Where(Available).OrderBy(StableKey, StringComparer.Ordinal))
                    pairs.Add(new DoorPair { Ship = door, Id = -1 });
            List<EntranceTeleport> activeShips = pairs.Where(p => p.Ship != null && Available(p.Ship)).Select(p => p.Ship).ToList();
            entrances.RemoveAll(d => d == null || IsShip(d));
            // LLL numbers in reverse order. Prepending preserves the moon's existing IDs and gives the ship extra IDs at the end.
            entrances.InsertRange(0, activeShips);
            registered = activeShips.Count > 0;
        }

        internal static EntranceTeleport[] RegisterDawn()
        {
            // Dawn's native assignment is ascending, unlike LLL's reverse numbering.
            // Keep the moon's ordinary fire exits first, then append available ship doors.
            List<EntranceTeleport> entrances = FreshScan().Where(d => d.isEntranceToBuilding && d.entranceId > 0 && !IsShip(d)).ToList();
            List<EntranceTeleport> ships = pairs.Where(p => p.Ship != null && Available(p.Ship)).Select(p => p.Ship).ToList();
            entrances.AddRange(ships);
            registered = ships.Count > 0;
            return entrances.ToArray();
        }

        internal static void CaptureIds()
        {
            foreach (DoorPair pair in pairs) if (pair.Ship != null) pair.Id = pair.Ship.entranceId;
            if (registered) Log("Registered " + pairs.Count + " ship fire exit(s) for this landing: IDs=" + string.Join(",", pairs.Select(p => p.Id.ToString()).ToArray()) + ". Furniture parent preserved.");
        }

        internal static bool HasRegisteredShip { get { return registered; } }

        internal static void Resolve()
        {
            if (!registered) return;
            idsReady = true;
            EntranceTeleport[] doors = FreshScan();
            foreach (DoorPair pair in pairs)
            {
                if (pair.Interior != null && pair.Interior.exitScript == pair.Ship) ClearExit(pair.Interior);
                pair.Interior = null;
                if (pair.Ship == null) continue;
                EntranceTeleport[] inside = doors.Where(d => !d.isEntranceToBuilding && d.entranceId == pair.Id).ToArray();
                bool uniqueOutside = doors.Count(d => d.isEntranceToBuilding && d.entranceId == pair.Id && (!IsShip(d) || Available(d))) == 1;
                if (pair.Id <= 0 || !uniqueOutside || inside.Length != 1)
                {
                    ClearExit(pair.Ship);
                    if (!pair.Reported) Warn("Cannot pair ship fire exit ID=" + pair.Id + ": interior candidates=" + inside.Length + ", unique outside=" + uniqueOutside + ". It remains blocked instead of using a moon's normal fire exit.");
                    pair.Reported = true;
                    continue;
                }
                pair.Interior = inside[0];
                managedInteriors.Add(pair.Interior);
                pair.Ship.exitScript = pair.Interior;
                pair.Interior.exitScript = pair.Ship;
                if (!pair.Reported) Log("Paired ID=" + pair.Id + ": " + StableKey(pair.Ship) + " <-> " + StableKey(pair.Interior) + "; spawned=" + pair.Ship.IsSpawned + "/" + pair.Interior.IsSpawned + ".");
                pair.Reported = true;
            }
            UpdateInteraction();
        }

        internal static bool TryFind(EntranceTeleport source, out bool found)
        {
            found = false;
            DoorPair pair = pairs.FirstOrDefault(p => p.Ship == source || (p.Interior != null && p.Interior == source));
            if (pair == null)
            {
                if (IsShip(source) || managedInteriors.Contains(source)) { ClearExit(source); return true; }
                return false; // Leave ordinary moon entrances completely alone.
            }
            EntranceTeleport destination = source == pair.Ship ? pair.Interior : pair.Ship;
            int mode = Mode(); // Both=0, Entrance=1, Exit=2 in the original mod.
            bool allowedDirection = source == pair.Ship ? mode != 2 : mode != 1;
            found = registered && idsReady && Landed() && allowedDirection && Available(pair.Ship) && destination != null && destination.gameObject.activeInHierarchy && destination.entrancePoint != null && source.entranceId == pair.Id && destination.entranceId == pair.Id;
            source.exitScript = found ? destination : null;
            return true;
        }

        internal static void Reset()
        {
            foreach (DoorPair pair in pairs)
            {
                if (pair.Ship != null) { ClearExit(pair.Ship); SetInteraction(pair.Ship, false); }
                if (pair.Interior != null && pair.Interior.exitScript == pair.Ship) ClearExit(pair.Interior);
            }
            pairs.Clear();
            managedInteriors.Clear();
            registered = false;
            idsReady = false;
        }

        internal static void UpdateInteraction()
        {
            foreach (DoorPair pair in pairs)
            {
                if (pair.Ship == null) continue;
                bool active = registered && idsReady && Landed() && Available(pair.Ship) && pair.Interior != null;
                if (active != pair.Active) { SetInteraction(pair.Ship, active); pair.Active = active; }
            }
        }

        private static void ClearExit(EntranceTeleport door)
        {
            door.exitScript = null;
            if (gotExit != null) gotExit.SetValue(door, false);
            if (firstCheck != null) firstCheck.SetValue(door, false);
            if (noExit != null) noExit.SetValue(door, false);
        }

        private static void SetInteraction(EntranceTeleport door, bool active)
        {
            door.enabled = active;
            InteractTrigger trigger = door.GetComponent<InteractTrigger>();
            if (trigger != null) trigger.enabled = active;
            BoxCollider collider = door.GetComponent<BoxCollider>();
            if (collider != null) collider.enabled = active;
            // Warning is a sibling in each of the original three furniture variants.
            if (door.transform.parent != null)
            {
                Transform warning = door.transform.parent.Find("EntranceTeleportWarning");
                BoxCollider warningCollider = warning == null ? null : warning.GetComponent<BoxCollider>();
                if (warningCollider != null) warningCollider.enabled = !active;
            }
        }

        private static string StableKey(EntranceTeleport door)
        {
            string path = "";
            for (Transform t = door.transform; t != null; t = t.parent) path = "/" + t.name + "[" + t.GetSiblingIndex() + "]" + path;
            // Furniture's server-assigned network ID is identical on host and clients.
            Unity.Netcode.NetworkObject network = door.GetComponentInParent<Unity.Netcode.NetworkObject>();
            return network != null && network.IsSpawned ? "net:" + network.NetworkObjectId.ToString("D20") + path : door.gameObject.scene.name + path;
        }
    }
}
