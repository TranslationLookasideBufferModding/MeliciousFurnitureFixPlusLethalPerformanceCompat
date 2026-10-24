using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FireExitTests
{
    private const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type runtime, entranceType, patches, harmonyType, roundType;
    private static object harmony, round;
    private static int checks, mode;
    private static Type simulation;
    private static int SimulatedCacheCalls { get { return (int)simulation.GetField("Calls").GetValue(null); } }
    private static bool landed;
    private static readonly HashSet<Component> stored = new HashSet<Component>();
    private static readonly List<GameObject> objects = new List<GameObject>();
    private static Scene moonScene, shipScene, interiorScene;
    private static Assembly lll;
    private static Assembly furnitureAssembly;
    private static Assembly dawn;
    private static string Arg(string key) { string[] a=Environment.GetCommandLineArgs(); return a[Array.IndexOf(a,key)+1]; }
    public static void Run()
    {
        string managed=Arg("-managedPath"), core=Arg("-corePath"), plugins=Arg("-pluginsPath");
        string[] pluginDlls=Directory.GetFiles(plugins,"*.dll",SearchOption.AllDirectories);
        AppDomain.CurrentDomain.AssemblyResolve += (s,e)=> {
            string name=new AssemblyName(e.Name).Name+".dll";
            foreach(string dir in new[]{core,managed}) { string path=Path.Combine(dir,name);if(File.Exists(path))return Assembly.LoadFrom(path); }
            string plugin=pluginDlls.FirstOrDefault(p=>Path.GetFileName(p)==name);
            return plugin==null?null:Assembly.LoadFrom(plugin);
        };
        Assembly game=Assembly.LoadFrom(Path.Combine(managed,"Assembly-CSharp.dll"));
        entranceType=game.GetType("EntranceTeleport",true);
        roundType=game.GetType("RoundManager",true);
        furnitureAssembly=Assembly.LoadFrom(Arg("-furniturePath"));
        lll=Assembly.LoadFrom(Arg("-lllPath"));
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-dawnPath")>=0)dawn=Assembly.LoadFrom(Arg("-dawnPath"));
        Assembly addon=Assembly.LoadFrom(Arg("-pluginPath"));
        runtime=addon.GetType("MliciosFurnitureFixPlus.Runtime",true);
        patches=addon.GetType("MliciosFurnitureFixPlus.Patches",true);
        runtime.GetField("Landed",All).SetValue(null,new Func<bool>(()=>landed));
        runtime.GetField("Mode",All).SetValue(null,new Func<int>(()=>mode));
        runtime.GetField("Log",All).SetValue(null,new Action<string>(t=>Debug.Log("ADDON "+t)));
        runtime.GetField("Warn",All).SetValue(null,new Action<string>(t=>Debug.Log("ADDON WARNING "+t)));
        ParameterExpression p=Expression.Parameter(entranceType,"door");
        Delegate available=Expression.Lambda(typeof(Func<,>).MakeGenericType(entranceType,typeof(bool)),Expression.Call(typeof(FireExitTests).GetMethod("Available",All),Expression.Convert(p,typeof(Component))),p).Compile();
        runtime.GetField("Available",All).SetValue(null,available);
        harmonyType=Assembly.LoadFrom(Path.Combine(core,"0Harmony.dll")).GetType("HarmonyLib.Harmony",true);
        harmony=Activator.CreateInstance(harmonyType,new object[]{"MliciosFurnitureFixPlus.Tests"});
        try
        {
            // Prevent dependency logging from requiring a live BepInEx plugin instance.
            Patch(lll.GetType("LethalLevelLoader.DebugHelper").GetMethod("Log",All),"SkipLog");
            // Simulate the missing/unspawned-object lookup: managed pairs must bypass it, normal doors must still use it.
            simulation=Assembly.LoadFrom(Arg("-supportPath")).GetType("CacheSimulation");
            Type harmonyMethod=harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod");
            object simulationHook=Activator.CreateInstance(harmonyMethod,new object[]{simulation.GetMethod("Transpiler")});
            harmonyType.GetMethods().Single(m=>m.Name=="Patch" && m.GetParameters().Length==6).Invoke(harmony,new object[]{entranceType.GetMethod("FindExitPoint",All),null,null,simulationHook,null,null});
            MethodInfo oldPrefix=furnitureAssembly.GetType("FireExitPatch").GetMethod("PreLevelLoad",All);
            object oldHook=Activator.CreateInstance(harmonyMethod,new object[]{oldPrefix});
            MethodInfo load=roundType.GetMethod("LoadNewLevel",All);
            harmonyType.GetMethods().Single(m=>m.Name=="Patch" && m.GetParameters().Length==6).Invoke(harmony,new object[]{load,oldHook,null,null,null,null});
            Patch(load,"SkipLog"); // Unrelated prefix must survive targeted unpatching.
            patches.GetMethod("Install",All).Invoke(null,new[]{harmony});
            object info=harmonyType.GetMethod("GetPatchInfo").Invoke(null,new object[]{load});
            object[] prefixes=((IEnumerable)info.GetType().GetField("Prefixes").GetValue(info)).Cast<object>().ToArray();
            Check(!prefixes.Any(x=>(MethodInfo)x.GetType().GetProperty("PatchMethod").GetValue(x,null)==oldPrefix),"only original furniture landing prefix removed from RoundManager");
            Check(prefixes.Any(x=>((MethodInfo)x.GetType().GetProperty("PatchMethod").GetValue(x,null)).Name=="SkipLog"),"unrelated patches survive selective unpatching");
            oldPrefix.Invoke(null,null);
            Check(furnitureAssembly.GetType("FireExitPatch").GetField("fireEntrance",All).GetValue(null)==null,"original helper body is also suppressed if called directly");
            round=FormatterServices.GetUninitializedObject(roundType);
            moonScene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(moonScene,"Assets/TestMoon.unity");
            shipScene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(shipScene,"Assets/TestShip.unity");
            interiorScene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(interiorScene,"Assets/TestInterior.unity");
            lll.GetType("LethalLevelLoader.LevelLoader").GetField("currentLevelScene",All).SetValue(null,moonScene);
            Component ship=Door("EntranceTeleportShip0",true,20,shipScene,Vector3.zero,true);
            Check(!((Behaviour)ship).enabled,"newly created ship door stays disabled in orbit");
            Transform originalParent=ship.transform.parent;
            Component main=Door("MoonMain",true,0,moonScene,new Vector3(0,0,50));
            Component outside=Door("MoonFire",true,1,moonScene,new Vector3(20,0,50));
            landed=false;mode=0;
            Run("Prepare");
            Check(!((Behaviour)ship).enabled,"ship door disabled before landing");
            Generate();
            Check(Id(ship)==2 && Id(outside)==1 && Id(main)==0,"real LLL preserves moon IDs and adds ship ID 2");
            Check(ship.transform.parent==originalParent,"furniture parent unchanged by real LLL registration");
            Component mainIn=Door("InsideMain",false,0,interiorScene,new Vector3(0,0,-100));
            Component inside1=Door("InsideFire1",false,1,interiorScene,new Vector3(10,0,-100));
            Component inside2=Door("InsideFire2",false,1,interiorScene,new Vector3(30,0,-100));
            Array fresh=(Array)runtime.GetMethod("FreshScan",All).Invoke(null,null);
            int[] scanned=fresh.Cast<Component>().Select(d=>d.GetInstanceID()).ToArray();
            Check(scanned.SequenceEqual(scanned.OrderBy(i=>i)),"fresh enumeration preserves native InstanceID ordering for distance ties");
            SetIds(new Vector3(0,0,-100));
            Check(Id(inside1)==1 && Id(inside2)==2,"real v81 SetExitIDs enumerates unspawned scene doors");
            Check(!Find(ship),"ship blocked until landing completes");
            landed=true;Run("UpdateInteraction");
            Check(((Behaviour)ship).enabled && ship.GetComponent<BoxCollider>().enabled,"host or client activation on landing");
            Check(!ship.transform.parent.Find("EntranceTeleportWarning").GetComponent<BoxCollider>().enabled,"wait-warning collider disabled when ready");
            Check(Find(ship) && Exit(ship)==inside2,"ship finds dedicated interior despite missing-object cache simulation");
            Check(Find(inside2) && Exit(inside2)==ship,"interior returns to ship");
            Check(SimulatedCacheCalls==0,"managed pair never invokes simulated global cache");
            Check(!Find(inside1) && SimulatedCacheCalls==1,"ordinary interior fire exit retains original/cache lookup");
            Check(Id(outside)==1 && Id(inside1)==1,"normal moon fire exit not stolen");
            Run("Reset");
            Check(Exit(ship)==null && Exit(inside2)==null,"takeoff clears reciprocal cached exits");
            Check(!Find(ship),"takeoff cannot reuse previous destination");
            UnityEngine.Object.DestroyImmediate(main.gameObject);UnityEngine.Object.DestroyImmediate(outside.gameObject);
            UnityEngine.Object.DestroyImmediate(mainIn.gameObject);UnityEngine.Object.DestroyImmediate(inside1.gameObject);UnityEngine.Object.DestroyImmediate(inside2.gameObject);
            // Second moon has two fire exits, like Volition. The ship must get a NEW ID.
            main=Door("VolitionMain",true,0,moonScene,new Vector3(0,0,50));
            outside=Door("VolitionFire1",true,1,moonScene,new Vector3(20,0,50));
            Component outside2=Door("VolitionFire2",true,2,moonScene,new Vector3(40,0,50));
            landed=false;Generate(dawn!=null);
            Check(Id(ship)==3 && new[]{Id(outside),Id(outside2)}.OrderBy(x=>x).SequenceEqual(new[]{1,2}),"LLL-to-Dawn transition without LLL callback assigns fresh ship ID 3");
            inside1=Door("SecondInside1",false,1,interiorScene,new Vector3(10,0,-100));
            inside2=Door("SecondInside2",false,1,interiorScene,new Vector3(20,0,-100));
            Component inside3=Door("SecondInside3",false,1,interiorScene,new Vector3(30,0,-100));
            SetIds(new Vector3(0,0,-100));landed=true;Run("UpdateInteraction");
            Check(Find(ship) && Exit(ship)==inside3,"second landing enters newly paired interior");
            Check(Find(inside3) && Exit(inside3)==ship,"second landing exits to ship rather than Volition fire exit");
            Check(Exit(ship)!=inside2 && Exit(inside3)!=outside,"previous ID 2 cannot redirect return trip");
            Check(Id(outside)!=Id(ship) && Id(outside2)!=Id(ship),"Dawn keeps both normal moon exits distinct from the ship");
            mode=1;Check(Find(ship) && !Find(inside3),"Entrance-only mode honored");
            mode=2;Check(!Find(ship) && Find(inside3),"Exit-only mode honored");
            mode=0;Check(Find(ship) && Find(inside3),"Both mode honored");
            stored.Add(ship);Run("UpdateInteraction");
            Check(!Find(ship) && !Find(inside3) && !ship.GetComponent<BoxCollider>().enabled,"stored furniture cannot teleport either direction");
            stored.Remove(ship);Run("UpdateInteraction");
            Check(Find(ship),"retrieved furniture can use its existing same-landing pair");
            landed=false;Run("UpdateInteraction");
            Check(!Find(ship) && ship.transform.parent.Find("EntranceTeleportWarning").GetComponent<BoxCollider>().enabled,"orbit disables exit and restores warning");
            landed=true;Run("UpdateInteraction");
            // Foreign duplicate ID: block instead of arbitrarily selecting a normal fire exit.
            Component duplicate=Door("ConflictingOutside",true,Id(ship),moonScene,new Vector3(60,0,50));
            Run("Resolve");Check(!Find(ship),"duplicate outside ID blocked instead of misrouting");
            Check(!Find(inside3) && Exit(inside3)==null,"duplicate outside ID also blocks the interior return route");
            UnityEngine.Object.DestroyImmediate(duplicate.gameObject);Run("Resolve");Check(Find(ship),"unambiguous pair can recover");
            entranceType.GetField("entrancePoint",All).SetValue(ship,null);
            Check(!Find(inside3),"missing return destination does not dereference null");
            UnityEngine.Object.DestroyImmediate(inside3.gameObject);Run("Resolve");
            Check(!Find(ship),"missing interior does not silently use another door");
            Run("Reset");stored.Add(ship);Generate(dawn!=null);
            Check(!(bool)runtime.GetProperty("HasRegisteredShip",All).GetValue(null,null),"stored ship exit excluded from dungeon generation");
            Check(ship.transform.parent==originalParent,"parent remains unchanged after two landings and storage");
            if(dawn!=null)
            {
                stored.Remove(ship);landed=false;Run("Reset");
                UnityEngine.Object.DestroyImmediate(inside1.gameObject);UnityEngine.Object.DestroyImmediate(inside2.gameObject);
                entranceType.GetField("entrancePoint",All).SetValue(ship,ship.transform);
                Generate();Generate(true); // Dawn's runtime hook may run after LLL in the SAME generation.
                Check(Id(ship)==3 && (bool)runtime.GetProperty("HasRegisteredShip",All).GetValue(null,null),"Dawn after LLL in the same generation retains fresh ship registration");
                inside1=Door("ThirdInside1",false,1,interiorScene,new Vector3(10,0,-100));
                inside2=Door("ThirdInside2",false,1,interiorScene,new Vector3(20,0,-100));
                inside3=Door("ThirdInside3",false,1,interiorScene,new Vector3(30,0,-100));
                SetIds(new Vector3(0,0,-100));landed=true;Run("UpdateInteraction");
                Check(Find(ship) && Exit(ship)==inside3 && Find(inside3) && Exit(inside3)==ship,"combined LLL and Dawn hooks allow both directions after a third landing");
            }
            Debug.Log("FIRE_EXIT_TESTS_OK "+checks+" checks; real LLL generation + "+(dawn==null?"no DawnLib installed":"real DawnLib transition without LLL callback")+" + real v81 SetExitIDs + simulated LP-style missing-object lookup. Actual multiplayer/LP lobby test pending.");
        }
        finally
        {
            Run("Reset");harmonyType.GetMethod("UnpatchSelf").Invoke(harmony,null);
            foreach(GameObject go in objects)if(go!=null)UnityEngine.Object.DestroyImmediate(go);
        }
    }
    private static bool Available(Component door) { return door!=null && door.gameObject.activeInHierarchy && !stored.Contains(door); }
    private static bool SkipLog() { return false; }
    private static void Patch(MethodBase target,string prefix,int priority=800)
    {
        Type hm=harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod");
        object hook=Activator.CreateInstance(hm,new object[]{typeof(FireExitTests).GetMethod(prefix,All)});
        hm.GetField("priority").SetValue(hook,priority);
        MethodInfo patch=harmonyType.GetMethods().Single(m=>m.Name=="Patch" && m.GetParameters().Length==6);
        patch.Invoke(harmony,new object[]{target,hook,null,null,null,null});
    }
    private static Component Door(string name,bool outside,int id,Scene scene,Vector3 position,bool ship=false)
    {
        GameObject root=new GameObject(ship?"FireExit0":name+"Root");objects.Add(root);SceneManager.MoveGameObjectToScene(root,scene);
        GameObject go=new GameObject(name);go.transform.SetParent(root.transform);go.transform.position=position;
        Component door=go.AddComponent(entranceType);
        entranceType.GetField("isEntranceToBuilding",All).SetValue(door,outside);
        entranceType.GetField("entranceId",All).SetValue(door,id);
        entranceType.GetField("entrancePoint",All).SetValue(door,go.transform);
        if(ship) {go.AddComponent<BoxCollider>();GameObject warning=new GameObject("EntranceTeleportWarning");warning.transform.SetParent(root.transform);warning.AddComponent<BoxCollider>();}
        // Editor tests do not automatically run game MonoBehaviour.Awake. Serialized fields precede Awake in a prefab spawn.
        entranceType.GetMethod("Awake",All).Invoke(door,null);
        return door;
    }
    private static void Generate(bool useDawn=false)
    {
        Assembly dun=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="DunGen")??Assembly.Load("DunGen");
        Type generatorType=dun.GetType("DunGen.DungeonGenerator",true), flowType=dun.GetType("DunGen.Graph.DungeonFlow",true);
        object generator=Activator.CreateInstance(generatorType);
        ScriptableObject flow=ScriptableObject.CreateInstance(flowType);
        object prop=Activator.CreateInstance(flowType.GetNestedType("GlobalPropSettings",BindingFlags.Public|BindingFlags.NonPublic));
        prop.GetType().GetField("ID",All).SetValue(prop,1231);
        prop.GetType().GetField("Count",All).SetValue(prop,Activator.CreateInstance(dun.GetType("DunGen.IntRange"),new object[]{0,0}));
        ((IList)flowType.GetField("GlobalProps",All).GetValue(flow)).Add(prop);
        generatorType.GetField("DungeonFlow",All).SetValue(generator,flow);
        ScriptableObject level=ScriptableObject.CreateInstance(lll.GetType("LethalLevelLoader.ExtendedLevel",true));
        level.GetType().GetField("<NumberlessPlanetName>k__BackingField",All).SetValue(level,"TestMoon");
        int expectedExits;
        if(useDawn)
        {
            dawn.GetType("Dawn.DungeonRegistrationHandler",true).GetMethod("AdjustFireExits",All).Invoke(null,new object[]{flow});
            Array scanned=(Array)runtime.GetMethod("FreshScan",All).Invoke(null,null);
            expectedExits=scanned.Cast<Component>().Count(d=>(bool)entranceType.GetField("isEntranceToBuilding",All).GetValue(d) && Id(d)>0 && Available(d));
        }
        else
        {
            lll.GetType("LethalLevelLoader.DungeonLoader").GetMethod("PatchFireEscapes",All).Invoke(null,new object[]{generator,level});
            ICollection entrances=(ICollection)lll.GetType("LethalLevelLoader.DungeonLoader").GetMethod("GetEntranceTeleports",All).Invoke(null,null);
            expectedExits=entrances.Count-1;
        }
        object count=prop.GetType().GetField("Count",All).GetValue(prop);
        Check((int)count.GetType().GetField("Min",All).GetValue(count)==expectedExits && (int)count.GetType().GetField("Max",All).GetValue(count)==expectedExits,"real "+(useDawn?"DawnLib":"LLL")+" requests the correct number of generated interior fire exits");
        UnityEngine.Object.DestroyImmediate(level);UnityEngine.Object.DestroyImmediate(flow);
    }
    private static void SetIds(Vector3 main) { roundType.GetMethod("SetExitIDs",All).Invoke(round,new object[]{main}); }
    private static int Id(Component door) { return (int)entranceType.GetField("entranceId",All).GetValue(door); }
    private static Component Exit(Component door) { return (Component)entranceType.GetField("exitScript",All).GetValue(door); }
    private static bool Find(Component door) { return (bool)entranceType.GetMethod("FindExitPoint",All).Invoke(door,null); }
    private static object Run(string method,params object[] args) { return runtime.GetMethod(method,All).Invoke(null,args); }
    private static void Check(bool condition,string label) { if(!condition)throw new Exception("FAIL "+label);checks++;Debug.Log("PASS "+label); }
}
