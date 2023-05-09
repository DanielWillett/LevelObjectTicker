using JetBrains.Annotations;
using System.Reflection;
using Types = SDG.Unturned.Types;

namespace LevelObjectTicker;

public sealed class LevelObjectTicker : RocketPlugin<LevelObjectTickerConfiguration>
{
    private readonly List<LevelObject> _binaries = new List<LevelObject>(64);
    private float _lastTick;
    private float _resetTimeLimit;
    private float _tickSpeed;
    private bool _debugLogging;
    private bool _disabled;
    private Guid[] _objBlacklist = Array.Empty<Guid>();
    private Guid[] _objWhitelist = Array.Empty<Guid>();
    private bool _lvlLoaded;
    private static ClientStaticMethod<byte, byte, ushort, byte, bool, Vector3>? _sendObjectRubble;
    public static LevelObjectTicker Instance { get; private set; } = null!;
    protected override void Load()
    {
        Logger.Log($"Loading {Assembly.GetName().Name} v{Assembly.GetName().Version} (https://github.com/DanielWillett).");

        _disabled = Configuration.Instance.Disabled;
        _tickSpeed = Configuration.Instance.TickSpeed;
        _objBlacklist = Configuration.Instance.ObjectBlacklist ?? Array.Empty<Guid>();
        _objWhitelist = Configuration.Instance.ObjectWhitelist ?? Array.Empty<Guid>();
        if (_objBlacklist.Length == 1 && _objBlacklist[0] == Guid.Empty)
            _objBlacklist = Array.Empty<Guid>();
        if (_objWhitelist.Length == 1 && _objWhitelist[0] == Guid.Empty)
            _objWhitelist = Array.Empty<Guid>();
        _debugLogging = Configuration.Instance.DebugLogging;
        _resetTimeLimit = Configuration.Instance.ResetTimeLimit;
        bool save = false;
        if (_tickSpeed <= 0)
        {
            Logger.LogWarning($"Invalid tick speed: {_tickSpeed}, reset to {0.1}.");
            _tickSpeed = 0.1f;
            Configuration.Instance.TickSpeed = 0.1f;
            save = true;
        }
        if (_resetTimeLimit < -1)
        {
            Logger.LogWarning($"Invalid reset time limit: {_resetTimeLimit}, reset to {60f}.");
            _resetTimeLimit = 60f;
            Configuration.Instance.ResetTimeLimit = 60f;
            save = true;
        }
        else if (_resetTimeLimit <= 0) _resetTimeLimit = float.MaxValue;

        _sendObjectRubble = typeof(ObjectManager).GetField("SendObjectRubble", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as ClientStaticMethod<byte, byte, ushort, byte, bool, Vector3>;
        if (_sendObjectRubble == null)
        {
            Logger.LogWarning("Unable to reflect RPC 'ObjectManager.SendObjectRubble', this plugin is out of date and must be updated. Rubble objects will not be sent to clients when updated.");
        }

        if (_debugLogging)
        {
            Logger.Log("Debug logging enabled.");
        }


        if (save)
            Configuration.Save();

        Instance = this;
        _lvlLoaded = Level.isLoaded;
        if (_lvlLoaded)
            OnLevelLoaded(Level.BUILD_INDEX_GAME);
        else
            Level.onPostLevelLoaded += OnLevelLoaded;
    }


    protected override void Unload()
    {
        Logger.Log($"Unloading {Assembly.GetName().Name} (https://github.com/DanielWillett).");
        if (!_lvlLoaded)
            Level.onPostLevelLoaded -= OnLevelLoaded;
        Instance = null!;
        _objBlacklist = null!;
        _objWhitelist = null!;
        _binaries.Clear();
    }

    private void OnLevelLoaded(int level)
    {
        if (level != Level.BUILD_INDEX_GAME)
            return;
        if (!_lvlLoaded)
            Level.onPostLevelLoaded -= OnLevelLoaded;
        _lvlLoaded = true;
        _binaries.Clear();
        Logger.Log("Level Object Tracker registering objects...");
        int c = 0;
        for (int x = 0; x < Regions.WORLD_SIZE; ++x)
        {
            for (int y = 0; y < Regions.WORLD_SIZE; ++y)
            {
                List<LevelObject> region = LevelObjects.objects[x, y];
                for (int i = 0; i < region.Count; ++i)
                {
                    LevelObject obj = region[i];
                    if (obj.asset == null || obj.transform == null) continue;
                    bool wh = false;
                    for (int j = 0; j < _objWhitelist.Length; ++j)
                    {
                        if (_objWhitelist[j] == obj.asset.GUID)
                        {
                            wh = true;
                            break;
                        }
                    }
                    if (wh)
                    {
                        RegisterObject(obj, true, ref c);
                    }
                    else if (obj.asset.interactability == EObjectInteractability.BINARY_STATE &&
                             obj.interactable is InteractableObjectBinaryState state &&
                             state.objectAsset.interactabilityReset > 1f &&
                             state.objectAsset.interactabilityReset * Provider.modeConfigData.Objects.Binary_State_Reset_Multiplier < _resetTimeLimit)
                    {
                        RegisterObject(obj, false, ref c);
                    }
                    else if (obj.asset.interactability is EObjectInteractability.WATER or EObjectInteractability.FUEL &&
                        obj.interactable is InteractableObjectResource resx &&
                        resx.objectAsset.interactabilityReset > 1f &&
                        resx.objectAsset.interactabilityReset * (obj.asset.interactability == EObjectInteractability.WATER ? Provider.modeConfigData.Objects.Water_Reset_Multiplier : Provider.modeConfigData.Objects.Fuel_Reset_Multiplier) < _resetTimeLimit)
                    {
                        RegisterObject(obj, false, ref c);
                    }
                    else if (obj.rubble != null && obj.asset.rubbleReset > 1f && obj.asset.rubble == EObjectRubble.DESTROY &&
                             obj.asset.rubbleReset * Provider.modeConfigData.Objects.Rubble_Reset_Multiplier < _resetTimeLimit)
                    {
                        RegisterObject(obj, false, ref c);
                    }
                }
            }
        }

        Logger.Log($"Level Object Tracker registered {c} object(s) that fit the configured criteria.");
    }
    private void RegisterObject(LevelObject obj, bool wh, ref int c)
    {
        for (int j = 0; j < _objBlacklist.Length; ++j)
        {
            if (_objBlacklist[j] == obj.asset.GUID)
                goto skip;
        }
        _binaries.Add(obj);
        if (_debugLogging)
            Logger.Log($"Registered: {obj.asset.objectName} at {obj.transform.position}{(wh ? " (whitelisted)." : ".")}");
        ++c;
        return;
        skip:
        if (_debugLogging)
            Logger.Log($"Skipped blacklisted object: {obj.asset.objectName} at {obj.transform.position}.");
    }
    private void UpdateObject(LevelObject obj)
    {
        if (obj.asset.interactability == EObjectInteractability.BINARY_STATE && obj.interactable is InteractableObjectBinaryState state && state.checkCanReset(Provider.modeConfigData.Objects.Binary_State_Reset_Multiplier))
        {
            ObjectManager.forceObjectBinaryState(obj.transform, false);
            if (_debugLogging)
                Logger.Log($"Reseting: {obj.asset.objectName} at {obj.transform.position}.");
        }
        if (obj.asset.interactability is EObjectInteractability.WATER or EObjectInteractability.FUEL &&
            obj.interactable is InteractableObjectResource resx &&
            resx.checkCanReset(obj.asset.interactability == EObjectInteractability.WATER ? Provider.modeConfigData.Objects.Water_Reset_Multiplier : Provider.modeConfigData.Objects.Fuel_Reset_Multiplier))
        {
            ObjectManager.updateObjectResource(obj.transform, (ushort)Mathf.Min(resx.amount + (obj.asset.interactability == EObjectInteractability.WATER ? 1 : 500), resx.capacity), true);
            if (_debugLogging)
                Logger.Log($"Refilling: {obj.asset.objectName} at {obj.transform.position}.");
        }
        if (obj.rubble != null && obj.asset.rubble == EObjectRubble.DESTROY)
        {
            byte index;
            int ct = obj.rubble.getSectionCount();
            int c = 0;
            while ((index = obj.rubble.checkCanReset(Provider.modeConfigData.Objects.Rubble_Reset_Multiplier)) != byte.MaxValue && c <= ct)
            {
                ++c;
                obj.state[obj.state.Length - 1] |= Types.SHIFTS[index];
                if (ObjectManager.tryGetRegion(obj.transform, out byte x, out byte y, out ushort index2))
                {
                    if (_sendObjectRubble != null)
                        _sendObjectRubble.InvokeAndLoopback(ENetReliability.Reliable, ObjectManager.GatherRemoteClientConnections(x, y), x, y, index2, index, true, Vector3.zero);
                    else
                        ObjectManager.ReceiveObjectRubble(x, y, index2, index, true, Vector3.zero);
                    if (_debugLogging)
                        Logger.Log($"Repairing rubble: {obj.asset.objectName} at {obj.transform.position} (rubble #{index}).");
                }
            }
        }
    }

    [UsedImplicitly]
    private void Update()
    {
        if (_disabled || !_lvlLoaded) return;
        float time = Time.realtimeSinceStartup;
        if (time - _lastTick > _tickSpeed)
        {
            _lastTick = time;
            for (int i = 0; i < _binaries.Count; ++i)
            {
                UpdateObject(_binaries[i]);
            }
        }
    }
}