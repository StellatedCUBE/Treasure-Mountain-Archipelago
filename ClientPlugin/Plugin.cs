using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Achievements;
using Archipelago.MultiClient.Net;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using GameFlowControlKit.StateManagement;
using HarmonyLib;
using LitMotion;
using Save;
using SuikaBilliards.GameFlow;
using SuikaBilliards.GameFlow.States;
using SuikaBilliards.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ClientPlugin;

[BepInPlugin("moe.shinten.treasuremountainarchipelago", "Archipelago", "0.1.0")]
public class Plugin : BasePlugin {
    internal static Sprite archipelagoIcon, archipelagoIconYellow, archipelagoIconRed, archipelagoIconBlue, archipelagoIconGreen, archipelagoIconGrey;
    internal static new ManualLogSource Log;
    static readonly List<Ticker> tickers = [], tickersToAdd = [];

    internal static bool isGameLoaded = false;
    internal static bool isGameInProgress = false;

    internal static ArchipelagoSession archipelagoSession;
    internal static StateTree<GameFlowController> stateMachine;
    internal static GameTitleState titleState;

    public static void L(object o) => Log.LogInfo(o);

    public override void Load() {
        // Plugin startup logic
        Log = base.Log;
        #if DEBUG
        AddTicker(new Debug());
        #endif
        AddTicker(new APSetup());
        Harmony.CreateAndPatchAll(typeof(Plugin), "ppp");
    }

    internal static void Start(ArchipelagoSession apSession) {
        SaveData.Instance.Save();

        archipelagoSession = apSession;
        Harmony.CreateAndPatchAll(typeof(HarmonyPatches));
        AddTicker(new Console());
        AddTicker(new WeddingChecker());
        AddTicker(new Checks());
        AddTicker(new Items());
        AddTicker(new GoalCheck());
        AddTicker(new NoVS());
        if (APSlot.instance.deathLink)
            AddTicker(new DeathLink());
        AchievementManager.Instance.ClearData();
        AchievementManager.Instance.ClearAllAchievement();
        SaveData.Instance.Load();
        AchievementManager.Instance.InitializeTask();
    }

    internal static void LoadData() {
        L("Loading sprite data");
        var iconData = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Archipelago.png"));
        Texture2D iconTexture = new(2, 2, TextureFormat.RGBA32, false);
        ImageConversion.LoadImage(iconTexture, iconData);
        int s = iconTexture.height >> 1;
        archipelagoIcon = Sprite.Create(iconTexture, new(0, 0, s, s), new());
        archipelagoIconYellow = Sprite.Create(iconTexture, new(s, 0, s, s), new());
        archipelagoIconRed = Sprite.Create(iconTexture, new(s * 2, 0, s, s), new());
        archipelagoIconBlue = Sprite.Create(iconTexture, new(s * 3, 0, s, s), new());
        archipelagoIconGreen = Sprite.Create(iconTexture, new(0, s, s, s), new());
        archipelagoIconGrey = Sprite.Create(iconTexture, new(s, s, s, s), new());
        Checks.collected = Sprite.Create(iconTexture, new(s * 2, s, s, s), new());
        Checks.mark = Sprite.Create(iconTexture, new(s * 3, s, s, s), new());
    }

    [HarmonyPatch(typeof(MotionDispatcher), "Update")]
	[HarmonyPrefix]
    static void Tick() {
        isGameLoaded = AchievementManager.Instance;
        
        tickers.AddRange(tickersToAdd);
        tickersToAdd.Clear();

        foreach (var ticker in tickers)
            ticker.Tick();
        
        tickers.RemoveAll(ticker => ticker.removalQueued);
    }

    [HarmonyPatch(typeof(GameTitleState), "MakeButtonHandle")]
    [HarmonyPrefix]
    static void SaveSM(GameTitleState __instance) {
        titleState = __instance;
        stateMachine = __instance.CurrentLayer;

        Items.ticket = GameObject.Find("TicketDistributeConfirmationUI").transform
            .Find("Root/Contant/Frame/Frame/Ticket/TicketImage").GetComponent<Image>().sprite;
    }
    
    public static void AddTicker(Ticker ticker) => tickersToAdd.Add(ticker);

    #if DEBUG
    public static void AddTickerUniqueType(Ticker ticker) {
        var type = ticker.GetType();
        if (!tickers.Concat(tickersToAdd).Any(t => t.GetType() == type && !t.removalQueued))
            AddTicker(ticker);
    }
    #endif
}