﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Bots.Grind;
using Bots.ArchaeologyBuddy;
//using Gatherbuddy;
using Levelbot;
using CommonBehaviors.Actions;
using PoolFishingBuddy.Forms;
using Styx;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.AreaManagement;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles;
using Styx.Logic.Inventory.Frames.LootFrame;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals.World;
using TreeSharp;
using Action = TreeSharp.Action;


namespace PoolFishingBuddy
{
    public class PoolFisher : BotBase
    {
        #region variables

        private Composite _root;
        static public int _currenthotspot;
        static public WoWPoint _modHotspot;
        
        static public bool looking4NewPoint;
        static public bool looking4NewPool;
        static public bool need2Lure;
        static public bool MeIsFishing;
        static public bool bounceBack = false;
        static public Thread MonitoringThread;
        static public Thread GetValuesThread;

        static public Thread TempThread1;
        static public Thread TempThread2;

        static public int Ping;
        static public int newLocAttempts = 0;
        static public int castAttempts = 0;
        static public Stopwatch lootTimer = new Stopwatch();
        static public Stopwatch movetopoolTimer = new Stopwatch();
        static public volatile List<ulong> PermaBlacklist = new List<ulong>();
        static public WoWGameObject Pool = null;
        static public WoWFishingBobber Bobber = null;
        static public List<WoWPoint> PoolPoints = new List<WoWPoint>(100);
        static public List<WoWPoint> tempPoolPoints = new List<WoWPoint>(100);
        static public List<WoWPoint> badPoolPoints = new List<WoWPoint>(100);
        static public WoWPoint PoolPoint;
        static public WoWPoint WaterSurface;

        static public List<WoWItem> mainhandList = new List<WoWItem>();
        static public List<WoWItem> offhandList = new List<WoWItem>();
        static public List<WoWItem> poleList = new List<WoWItem>();
        static public List<WoWItem> BagItems = new List<WoWItem>();
        static public WoWItem Heartstone = Helpers.GetIteminBag(6948);
        
        #endregion

        static public List<WoWPoint> HotspotList;
        static public List<WoWPoint> BlackspotList;

        static public GrindArea GrindArea { get; set; }


        #region Overrides of BotBase

        private readonly Version _version = new Version(1, 0, 18);

        public override string Name
        {
            get { return "PoolFisher " + _version; }
        }

        public override PulseFlags PulseFlags { get { return PulseFlags.All; } }

        public override Form ConfigurationForm { get { return new FormFishConfig(); } }

        public override void Start()
        {
            GrindArea = ProfileManager.CurrentProfile.GrindArea;
            HotspotList = GrindArea.Hotspots.ConvertAll<WoWPoint>(hs => hs.ToWoWPoint());
            BlackspotList = ProfileManager.CurrentProfile.Blackspots.ConvertAll<WoWPoint>(bs => bs.Location);

            ProtectedItemsManager.ReloadProtectedItems();
            ForceMailManager.ReloadProtectedItems();

            looking4NewPoint = false;
            looking4NewPool = true;
            MeIsFishing = false;
            need2Lure = false;
            castAttempts = 0;
            newLocAttempts = 0;
            PoolPoints.Clear();
            Pool = null;
            _currenthotspot = -1;

            WoWSpell Mount = WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID);

            Styx.BotEvents.OnBotStart += Helpers.Init;
            Styx.BotEvents.OnBotStop += Helpers.Final;
            if (TreeRoot.IsRunning)
                Helpers.Init(new System.EventArgs());

            StyxSettings.Instance.LogoutForInactivity = false;

            Logging.Write(System.Drawing.Color.Blue, "{0} - Pool Fisher {1} starting!", Helpers.TimeNow, _version);
        }

        public override void Stop()
        {
            Logging.Write(System.Drawing.Color.Blue, "{0} - Pool Fisher {1} stopped!", Helpers.TimeNow, _version);
            StyxSettings.Instance.LogoutForInactivity = true;
        }

        public override Composite Root
        {
            get
            {
                return _root ?? (_root = 
                    new PrioritySelector(

                        new Decorator(ret => StyxWoW.Me == null || !StyxWoW.IsInGame || !StyxWoW.IsInWorld,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - [INVALID] Waiting", Helpers.TimeNow)),
                                new ActionSleep(10000))),

                        new Decorator(ret => StyxWoW.Me.IsFalling,
                            new ActionSleep(1000)),

                        new Decorator(ret => StyxWoW.Me.Dead || StyxWoW.Me.IsGhost,
                            LevelBot.CreateDeathBehavior()),

                        new Decorator(ret => !StyxWoW.Me.Mounted && StyxWoW.Me.Combat,
                            new Sequence(
                                new Action(ret => MeIsFishing = false),
                                new Action(ret => newLocAttempts = 0),
                                new Action(ret => castAttempts = 0),
                                new Action(ret => Helpers.equipWeapon()),
                                LevelBot.CreateCombatBehavior())),

                        // ToDo: own need to rest check on top of this..
                        CreateFishingBehavior(),
                        CreateRestBehavior(),


                        #region Buffs

                            new PrioritySelector(
                                // Use the bt
                                new Decorator(ctx => RoutineManager.Current.PreCombatBuffBehavior != null,
                                    RoutineManager.Current.PreCombatBuffBehavior),
                                // don't use the bt
                                new Decorator(
                                    ctx => RoutineManager.Current.NeedPreCombatBuffs,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Applying pre-combat buffs"),
                                        new Action(ret => RoutineManager.Current.PreCombatBuff())
                                        ))),

                        #endregion
                        
                        LevelBot.CreateLootBehavior(),

                        new Decorator(ret => StyxWoW.Me.FreeBagSlots <= ProfileManager.CurrentProfile.MinFreeBagSlots && PoolFisherSettings.Instance.ShouldMail,
                            new Sequence(
                                new Action(ret => Global.ShouldMail = true),
                                new Action(ret => Vendors.ForceRepair = true),
                                new Action(ret => Vendors.ForceMail = true),
                                new Action(ret => Vendors.ForceSell = true),
                                new Action(ret => Vendors.ForceTrainer = true),
                                new Action(ret => MeIsFishing = false),
                                new Action(ret => newLocAttempts = 0),
                                new Action(ret => castAttempts = 0),
                                new Action(ret => Helpers.equipWeapon()),
                                //CreateMountBehaviour(),
                                CreateRestBehavior(),
                                LevelBot.CreateVendorBehavior()
                            
                            )),

                        new Decorator(ret => StyxWoW.Me.FreeBagSlots <= ProfileManager.CurrentProfile.MinFreeBagSlots && !PoolFisherSettings.Instance.ShouldMail,
                            new PrioritySelector(

                                new Decorator(ret => Heartstone.Cooldown == 0,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Bags are full. Heart and Exit now!", Helpers.TimeNow)),
                                        new Action(ret => Heartstone.Use())
                                        //new Wait(30, ret => !StyxWoW.Me.IsCasting, new ActionIdle())
                                    )),

                                new Decorator(ret => Heartstone.Cooldown > 0,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Bags are full. Exit now!", Helpers.TimeNow)),
                                        new Action(ret => Helpers.quitWoW()),
                                        //new Wait(30, ret => !StyxWoW.IsInGame, new ActionIdle()),
                                        new Action(ret => TreeRoot.Stop())
                                    ))
                        )),

                        new Decorator(ret => !MeIsFishing && StyxWoW.Me.FreeBagSlots > ProfileManager.CurrentProfile.MinFreeBagSlots,
                            new PrioritySelector(

                                CreateMoveToPoolBehavior(),

                                new Decorator(ret => PoolPoints.Count == 0,
                                    new PrioritySelector(

                                        CreatePathBehavior(),
                                        CreateLookForPoolBehavior()
                                    
                                    ))
                            ))
                        ));
            }
        }

        #endregion

        private Composite CreateLookForPoolBehavior()
        {
            return new Decorator(ret => Helpers.findPool && looking4NewPool && Pool.X != 0,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateLookForPoolsBehaviour", Helpers.TimeNow))
                    ));
        }

        private Composite CreateMoveToPoolBehavior()
        {
            return new Decorator(ret => Pool != null && !MeIsFishing && !Blacklist.Contains(Pool.Guid) && !PoolFisher.PermaBlacklist.Contains(Pool.Entry),
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMoveToPoolBehaviour", Helpers.TimeNow)),
                    new PrioritySelector(

                        // Blacklist if other Player is detected
                        new Decorator(ret => Helpers.PlayerDetected && !PoolFisherSettings.Instance.NinjaPools,
                            new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Detected another player in pool range, blacklisting for 2 minutes.", Helpers.TimeNow)),
                                    new Action(ret => Helpers.BlackListPool()),
                                    new Action(delegate { return RunStatus.Success; })
                        )),

                        // Get PoolPoint
                        new Decorator(ret => looking4NewPoint,
                            new Sequence(
                                new Action(ret => WoWMovement.MoveStop()),
                                new Action(ret => Helpers.PoolPoint()))),

                        // Blacklist if Navigator can't generate Path
                        new Decorator(ret => PoolPoints.Count == 0,
                            new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - No path found to {1}, blacklisting for 2 minutes. (PoolFisher)", Helpers.TimeNow, Pool.Name)),
                                    new Action(ret => Helpers.BlackListPool()),
                                    new Action(delegate { return RunStatus.Success; })
                        )),

                        // Move to PoolPoint
                        new Decorator(pool => PoolPoints.Count > 0 && !looking4NewPoint,
                            new PrioritySelector(

                                // Blacklist if Navigator can't generate Path
                                new Decorator(ret => PoolPoints.Count == 0,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - No path found to {1}, blacklisting for 2 minutes. (PoolFisher)", Helpers.TimeNow, Pool.Name)),
                                        new Action(ret => Helpers.BlackListPool()),
                                        new Action(delegate { return RunStatus.Success; })
                                )),

                                // Pool still there?
                                new Decorator(ret => !Helpers.PoolIsStillThere,
                                    new Sequence(
                                        new Action(ret => Logging.Write("{0} - Fishing Pool is gone, moving on.", Helpers.TimeNow))
                                        )),

                                // reached max attempts for new locations?
                                new Decorator(ret => newLocAttempts == PoolFisherSettings.Instance.MaxNewLocAttempts + 1,
                                    new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Reached max. attempts for new locations!", Helpers.TimeNow)),
                                    new Action(ret => Helpers.BlackListPool())
                                )),

                                // tries++
                                new Decorator(ret => StyxWoW.Me.Location.Distance(PoolPoints[0]) <= 2 && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsMoving,
                                    new Sequence(
                                        new Wait(2, ret => MeIsFishing, new ActionIdle()),
                                        new Action(ret => newLocAttempts++),
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving to new location.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts))
                                )),
                                            

                                // Dismount
                                new Decorator(ret => StyxWoW.Me.Location.Distance(PoolPoints[0]) <= 1 && StyxWoW.Me.Mounted, //&& !StyxWoW.Me.IsMoving,
                                    new Sequence(
                                        new Action(ret => Mount.Dismount()),
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Location {1}, PoolPoint: {2}, Distance: {3}", Helpers.TimeNow, StyxWoW.Me.Location, PoolPoints[0], StyxWoW.Me.Location.Distance(PoolPoints[0]))),
                                        new Wait(3, ret => StyxWoW.Me.Mounted, new ActionIdle()),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 500))
                                )),

                                // in Line Line of sight?
                                new Decorator(ret => StyxWoW.Me.Location.Distance2D(PoolPoints[0]) <= 2 && !Pool.InLineOfSight && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Mounted,
                                    new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Pool is not in Line of Sight!", Helpers.TimeNow)),
                                    new Action(ret => Helpers.BlacklistPoolPoint(PoolPoints[0])),
                                    new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                                    new Action(ret => newLocAttempts++),
                                    new Action(ret => looking4NewPoint = true)
                                )),

                                // swimming?
                                new Decorator(ret => StyxWoW.Me.Location.Distance2D(PoolPoints[0]) <= 5 && StyxWoW.Me.IsSwimming && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Mounted,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Swimming at {1}...", Helpers.TimeNow, StyxWoW.Me.Location)),
                                        new Action(ret => Helpers.BlacklistPoolPoint(PoolPoints[0])),
                                        new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                                        new Action(ret => newLocAttempts++),
                                        new Action(ret => looking4NewPoint = true)
                                )),

                                // Move without Mount
                                new Decorator(ret => StyxWoW.Me.Location.Distance(PoolPoints[0]) > 0.5 && StyxWoW.Me.Location.Distance(PoolPoints[0]) <= 10 && !StyxWoW.Me.Mounted && GameWorld.IsInLineOfSight(StyxWoW.Me.Location, PoolPoints[0]),
                                    new PrioritySelector(

                                        new Decorator(ret => !Navigator.CanNavigateFully(StyxWoW.Me.Location, PoolPoints[0]),
                                            CreateMountBehavior()),

                                        new Sequence(
                                            new ActionSetActivity(ret => "Moving to PoolPoint: " + PoolPoints[0] + ", Location: " + StyxWoW.Me.Location + ", distance: " + PoolPoints[0].Distance(StyxWoW.Me.Location) + "(Not Mounted)"),
                                            new Action(ret => Logging.Write("{0} - Moving to PoolPoint: {1}, Location: {2}, Distance: {3}. (Not Mounted)", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location, PoolPoints[0].Distance(StyxWoW.Me.Location))),
                                            // Move
                                            new Action(ret => Navigator.MoveTo(PoolPoints[0]))
                                            //new Wait(2, ret => !StyxWoW.Me.IsMoving || StyxWoW.Me.Location.Distance(PoolPoints[0]) < 1.5, new ActionIdle())
                                            //new Action(ret => newLocAttempts++),
                                            //new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts)))
                                        )
                                )),

                                // Move with Mount
                                new Decorator(ret => StyxWoW.Me.Location.Distance(PoolPoints[0]) > 10 || StyxWoW.Me.Mounted || ( StyxWoW.Me.Location.Distance(PoolPoints[0]) <= 10 && !GameWorld.IsInLineOfSight(StyxWoW.Me.Location, PoolPoints[0])),
                                    new Sequence(
                                        new ActionSetActivity(ret => "Moving to PoolPoint: " + PoolPoints[0] + ", Location: " + StyxWoW.Me.Location + ", distance: " + PoolPoints[0].Distance(StyxWoW.Me.Location) + "(Mounted)"),
                                        new Action(ret => Logging.Write("{0} - Moving to PoolPoint: {1}, Location: {2}, Distance: {3}. (Mounted)", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location, PoolPoints[0].Distance(StyxWoW.Me.Location))),
                                        new PrioritySelector(
                                            // Mount if not mounted
                                            CreateMountBehavior(),

                                            // Move
                                            new Sequence(
                                                new Action(ret => Flightor.MoveWithTrace(PoolPoints[0]))
                                                //new Wait(2, ret => !StyxWoW.Me.IsMoving || StyxWoW.Me.Location.Distance(PoolPoints[0]) < 2, new ActionIdle())
                                                //new Action(ret => newLocAttempts++),
                                                //new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts)))
                                            )
                                        )
                                ))
                            ))
                )));
        }

        private Composite CreateFishingBehavior()
        {
            return new Decorator(ret => PoolPoints.Count > 0 && StyxWoW.Me.Location.Distance(PoolPoints[0]) <= 2.5 && !looking4NewPool && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsSwimming && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Combat,// && Pool.InLineOfSight,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateFishingBehaviour", Helpers.TimeNow)),
                    new Action(ret => MeIsFishing = true),
                    new PrioritySelector(

                        // Wait for GCD
                        new Decorator(ret => StyxWoW.GlobalCooldown,
                            new ActionIdle()),

                        new Decorator(ret => Styx.StyxWoW.Me.GetAllAuras().Any(Aura => Aura.SpellId == 81096),
                            new Sequence(
                                new Action(ret => Logging.Write("{0} - Sleep while red mist is on me.", Helpers.TimeNow)),
                                new Action(ret => castAttempts = 0),
                                new Wait(30, ret => !Styx.StyxWoW.Me.GetAllAuras().Any(Aura => Aura.SpellId == 81096) || StyxWoW.Me.Combat, new ActionIdle())
                                )),

                        // Pool locations left?
                        /*
                        new Decorator(ret => PoolPoints.Count == 0,
                            new Sequence(
                                new Action(ret => Helpers.BlackListPool()),
                                new Action(delegate { return RunStatus.Success; }))),
                        */
                        // Pool still there?
                        new Decorator(ret => !Helpers.PoolIsStillThere,
                            new Sequence(
                                new Action(ret => Logging.Write("{0} - Fishing Pool is gone, moving on.", Helpers.TimeNow)),
                                new Action(ret => newLocAttempts = 0),
                                new Action(ret => Helpers.equipWeapon())
                                )),

                        // reached max attempts for casting?
                        new Decorator(ret => castAttempts == PoolFisherSettings.Instance.MaxCastAttempts,
                            new Sequence(
                                new Action(ret => Logging.Write("{0} - Tried to cast {1} times. Moving to new location.", Helpers.TimeNow, PoolFisherSettings.Instance.MaxCastAttempts)),
                                new Action(ret => Helpers.BlacklistPoolPoint(PoolPoints[0])),
                                new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                                new Action(ret => newLocAttempts++),
                                new Action(ret => looking4NewPoint = true),
                                new Action(ret => MeIsFishing = false),
                                new Action(ret => castAttempts = 0),
                                CreateMoveToPoolBehavior()
                        )),

                        // in Line Line of sight?
                        new Decorator(ret => !Pool.InLineOfSight,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Pool is not in Line of Sight! Moving to new point.", Helpers.TimeNow)),
                                new Action(ret => Helpers.BlacklistPoolPoint(PoolPoints[0])),
                                new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                                new Action(ret => newLocAttempts++),
                                new Action(ret => looking4NewPoint = true),
                                new Action(ret => MeIsFishing = false),
                                CreateMoveToPoolBehavior()
                        )),

                        // swimming?
                        new Decorator(ret => StyxWoW.Me.IsSwimming,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Swimming at {1}...", Helpers.TimeNow, StyxWoW.Me.Location)),
                                new Action(ret => Helpers.BlacklistPoolPoint(PoolPoints[0])),
                                new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                                new Action(ret => newLocAttempts++),
                                new Action(ret => looking4NewPoint = true),
                                new Action(ret => MeIsFishing = false),
                                CreateMoveToPoolBehavior()
                        )),


                        // Do we need to interact with the bobber?
                        new Decorator(ret => Helpers.BobberIsBobbing,
                            new Sequence(
                                // Interact with the bobber
                                new Action(delegate
                                {
                                    Logging.Write("{0} - Bobber is bobbing!", Helpers.TimeNow);
                                    WoWGameObject Bobber = Helpers.FishingBobber;

                                    Bobber.Interact();
                                    Bobber = null;
                                    Thread.Sleep((Ping * 2) + 200);
                                    // wait for loot frame to apear
                                    Logging.Write("{0} - Interact done, looting!", Helpers.TimeNow);

                                    if (LootFrame.Instance.IsVisible)
                                        Logging.Write("{0} - LootFrame is visible!", Helpers.TimeNow);

                                    lootTimer.Reset();
                                    lootTimer.Start();
                                    while (LootFrame.Instance == null || !LootFrame.Instance.IsVisible)
                                    {
                                        if (lootTimer.ElapsedMilliseconds > 5000)
                                        {
                                            Logging.Write("{0} - Loot timer elapsed!", Helpers.TimeNow);
                                            castAttempts = 0;
                                            return RunStatus.Failure;
                                        }
                                        Thread.Sleep(100);
                                    }
                                    /*
                                    for (int i = 0; i < Helpers.LootItems; i++)
                                    {
                                        var info = new LootSlotInfo(i);

                                        if (!info.Locked)
                                        {
                                            Logging.Write("Trying to loot #{0} of {1} Rarity:{2}",
                                                info.LootQuantity,
                                                info.LootName,
                                                info.LootRarity);

                                            Helpers.Loot(i);
                                        }
                                    }
                                    */
                                    Lua.DoString("for i=1,GetNumLootItems() do ConfirmLootSlot(i) LootSlot(i) end");
                                    // wait for lootframe to close
                                    while (LootFrame.Instance != null && LootFrame.Instance.IsVisible)
                                    {
                                        Thread.Sleep(100);
                                    }

                                    Logging.Write("{0} - Looting done!", Helpers.TimeNow);
                                    castAttempts = 0;
                                    return RunStatus.Success;
                                })

                                    /*
                                    Logging.Write("Got a bite!");
                                    WoWGameObject bobber = Helpers.FishingBobber;

                                    if (bobber != null)
                                        bobber.Interact();

                                    else
                                    {
                                        Logging.Write("bobber is null");
                                        return RunStatus.Failure;
                                    }
                                    triesCast = 0;
                                    return RunStatus.Success;
                                }),

                                // Wait for the lootframe
                                new Wait(5, ret => LootFrame.Instance.IsVisible,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Looting"),
                                        new Action(ret => StyxWoW.SleepForLagDuration())
                                        ))*/
                        )),

                        // Recast
                        new Decorator(ret => Helpers.IsFishing && !Helpers.BobberIsInTheHole && Helpers.PoolIsStillThere && castAttempts <= PoolFisherSettings.Instance.MaxCastAttempts,
                            new PrioritySelector(

                                // Luring
                                new Decorator(ret => PoolFisherSettings.Instance.useLure && !Helpers.LureIsOnPole && (!StyxWoW.Me.IsCasting || StyxWoW.Me.ChanneledCastingSpellId == 0),
                                    new Sequence(
                                        new Action(ret => need2Lure = true),
                                        new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 200)),
                                        new Action(ret => Helpers.applylure()),
                                        new Wait(3, ret => Helpers.LureIsOnPole, new ActionIdle()))),

                                // cast fishing
                                new Decorator(ret => (PoolFisherSettings.Instance.useLure && Helpers.LureIsOnPole) || !PoolFisherSettings.Instance.useLure,
                                    new Sequence(
                                        new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                        //new Action(ret => Thread.Sleep((Ping * 2) + 250)),
                                        new Action(ret => castAttempts++),
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Casting.. Attempt: {1} of {2}.", Helpers.TimeNow, castAttempts, PoolFisherSettings.Instance.MaxCastAttempts)),
                                        new Action(ret => TreeRoot.StatusText = "Cast Fishing"),
                                        new Action(ret => SpellManager.Cast("Fishing")),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 250)),
                                        new Wait(1, ret => !StyxWoW.Me.IsCasting, new ActionIdle())
                                    ))
                        )),

                        // Poolfishing
                        new Decorator(ret => !Helpers.IsFishing && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsSwimming && Helpers.PoolIsStillThere,
                            new PrioritySelector(

                                // equip pole
                                new Decorator(ret => !Helpers.equipFishingPole,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Could not find any fishing Poles!", Helpers.TimeNow)),
                                        new Action(ret => TreeRoot.Stop()))),

                                // Luring
                                new Decorator(ret => PoolFisherSettings.Instance.useLure && !Helpers.LureIsOnPole && (!StyxWoW.Me.IsCasting || StyxWoW.Me.ChanneledCastingSpellId == 0),
                                    new Sequence(
                                        new Action(ret => need2Lure = true),
                                        new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 200)),
                                        new Action(ret => Helpers.applylure()),
                                        new Wait(3, ret => Helpers.LureIsOnPole, new ActionIdle()))),

                                // cast fishing
                                new Decorator(ret => (PoolFisherSettings.Instance.useLure && Helpers.LureIsOnPole) || !PoolFisherSettings.Instance.useLure,
                                    new Sequence(
                                        new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 250)),
                                        new Action(ret => castAttempts++),
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Casting.. Attempt: {1} of {2}.", Helpers.TimeNow, castAttempts, PoolFisherSettings.Instance.MaxCastAttempts)),
                                        new Action(ret => TreeRoot.StatusText = "Cast Fishing"),
                                        new Action(ret => SpellManager.Cast("Fishing")),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 250)),
                                        new Wait(1, ret => !StyxWoW.Me.IsCasting, new ActionIdle())
                                    ))
                        )),

                        // The pool phased out
                        new Decorator(ret => PoolPoints.Count > 0 && PoolPoints[0].X == 0 && PoolPoints[0].Y == 0,
                            new Sequence(
                                new Action(ret => Helpers.BlackListPool()),
                                new Action(delegate { return RunStatus.Success; }))),

                        new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Waiting for bobber to splash"),
                            new ActionIdle())

                        )));
        }


        private Composite CreatePathBehavior()
        {
            return new PrioritySelector(

                new Decorator(ret => HotspotList.Count <= 0,
                    new Sequence(
                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Profile has no hotspots!", Helpers.TimeNow)),
                        new Action(ret => TreeRoot.Stop()))),

                new Decorator(ret => _currenthotspot < 0,
                    new Sequence(
                        new Action(ret => _currenthotspot = HotspotList.IndexOf(HotspotList.OrderBy(hs => StyxWoW.Me.Location.Distance(hs)).FirstOrDefault())),
                        new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                        new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot)) 
                        )),

                // bounce or circle mode?
                new PrioritySelector(

                    new Decorator(ret => !PoolFisherSettings.Instance.BounceMode,
                        new PrioritySelector(

                            new Decorator(ret => _currenthotspot >= HotspotList.Count,
                                new Sequence(
                                    new Action(ret => _currenthotspot = 0),
                                    new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                    new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot)) 
                                )),

                            new Decorator(ret => StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                new Sequence(
                                    new Action(ret => _currenthotspot++),
                                    //new Action(ret => Logging.Write("_currenthotspot: {0}, HotspotList.Count: {1}.", _currenthotspot, HotspotList.Count)),
                                    new Decorator(ret => _currenthotspot <= HotspotList.Count,
                                        new Sequence(
                                            new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                            new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot))
                                    ))
                                ))
                            )),


                    new Decorator(ret => PoolFisherSettings.Instance.BounceMode,
                        new PrioritySelector(

                            new Decorator(ret => _currenthotspot >= HotspotList.Count && StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                new Sequence(
                                    new Action(ret => bounceBack = true),
                                    new Action(ret => _currenthotspot--),
                                    new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                    new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot)) 
                                    )),

                            new Decorator(ret => _currenthotspot <= 0 && StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                new Sequence(
                                    new Action(ret => bounceBack = false),
                                    new Action(ret => _currenthotspot++),
                                    new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                    new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot)) 
                                    )),

                            new Decorator(ret => bounceBack && StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                new Sequence(
                                    new Action(ret => _currenthotspot--),
                                    new Decorator(ret => _currenthotspot >= 0,
                                        new Sequence(
                                            new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                            new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot))
                                            ))
                                )),

                            new Decorator(ret => !bounceBack && StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                new Sequence(
                                    new Action(ret => _currenthotspot++),
                                    new Decorator(ret => _currenthotspot <= HotspotList.Count,
                                        new Sequence(
                                            new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                            new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot))
                                            ))
                                ))
                            
                            ))
                ),

                new DecoratorSetContext<WoWPoint>(() => HotspotList[_currenthotspot], hotspot => true,
                    new Sequence(
                        //new Action(ret => Logging.Write("Moving to hotspot: {0}", ret)),
                        new ActionSetActivity(hotspot => "Moving to hotspot: " + _modHotspot + ", distance: " + _modHotspot.Distance(StyxWoW.Me.Location)),
                        CreateMoveBehavior())));
        }

        private Composite CreateMoveBehavior()
        {
            return new Sequence(
                new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMoveBehaviour", Helpers.TimeNow)),

                new PrioritySelector(

                    new Decorator(ret => !StyxWoW.Me.Mounted && !StyxWoW.Me.IsIndoors,
                        CreateMountBehavior()),
                    new ActionMove()));
        }

        private Composite CreateMountBehavior()
        {
            return new Decorator(ret => !StyxWoW.Me.Mounted && !StyxWoW.Me.Combat,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMountBehaviour", Helpers.TimeNow)),
                    new PrioritySelector(

                        new Decorator(ret => StyxWoW.Me.IsSwimming && StyxWoW.Me.Location.Z >= (Helpers.GetWaterSurface(StyxWoW.Me.Location) - 1) && PoolFisherSettings.Instance.FlyingMountID != 0 && WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).IsValid,
                            new Sequence(
                                new Action(ret => Logging.Write("{0} - Location: {1}", Helpers.TimeNow, StyxWoW.Me.Location.Z)),
                                new Action(ret => Logging.Write("{0} - Water Suface: {1}", Helpers.TimeNow, WaterSurface.Z)),
                                new Action(ret => WoWMovement.MoveStop()),
                                new Action(ret => Logging.Write("{0} - Mounting {1}.", Helpers.TimeNow, WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name)),
                                new ActionSetActivity(spell => "Casting " + WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name),
                                new Action(spell => WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Cast()),
                                new Wait(2, ret => !StyxWoW.Me.IsCasting, new ActionIdle()),
                                new Action(ret => Thread.Sleep(2000 + (Ping * 3))),
                                new Action(ret => StyxWoW.SleepForLagDuration())
                                )),

                        new Decorator(ret => StyxWoW.Me.IsSwimming && StyxWoW.Me.Location.Z < Helpers.GetWaterSurface(StyxWoW.Me.Location),
                            new Sequence(
                                    new Action(ret => WaterSurface = StyxWoW.Me.Location),
                                    new Action(ret => WaterSurface.Z = Helpers.GetWaterSurface(StyxWoW.Me.Location)),
                                    new Action(ret => Logging.Write("{0} - Location: {1}, Water Suface: {2}", Helpers.TimeNow, StyxWoW.Me.Location, WaterSurface)),
                                    new Action(ret => WoWMovement.ClickToMove(WaterSurface))
                                    //new Action(ret => Navigator.MoveTo(WaterSurface))
                                    
                                )),

                        new Decorator(ret => PoolFisherSettings.Instance.FlyingMountID != 0 && WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).IsValid,
                            new Sequence(
                                new Action(ret => WoWMovement.MoveStop()),
                                new Action(ret => Logging.Write("{0} - Mounting {1}.", Helpers.TimeNow, WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name)),
                                new ActionSetActivity(spell => "Casting " + WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name),
                                new Action(spell => WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Cast()),
                                new Wait(2, ret => !StyxWoW.Me.IsCasting, new ActionIdle()),
                                new Action(ret => Thread.Sleep(2000 + (Ping * 3))),
                                new Action(ret => StyxWoW.SleepForLagDuration())
                                    ))
                        )));
        }

        private Composite CreateCastBehavior()
        {
            return new Sequence(
                new Action(ret => Logging.WriteDebug("{0} - Composit: CreateCastBehaviour", Helpers.TimeNow)),
                CreateMoveStopBehavior(),
                new ActionSetActivity(spell => "Casting " + ((WoWSpell)spell).Name),
                new Action(spell => ((WoWSpell)spell).Cast()),
                new Wait(2, ret => !StyxWoW.Me.IsCasting, new ActionIdle()),
                new Action(ret => StyxWoW.SleepForLagDuration()));
        }

        private Composite CreateMoveStopBehavior()
        {
            return new Decorator(ret => StyxWoW.Me.IsMoving,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMoveStopBehaviour", Helpers.TimeNow)),
                    new Action(ret => Logging.Write("{0} - IsMoving returned {1}. Stop moving!", Helpers.TimeNow, StyxWoW.Me.IsMoving)),
                    new Action(ret => WoWMovement.MoveStop()),
                    new Decorator(ret => !StyxWoW.Me.IsMoving,
                        new Action(delegate { return RunStatus.Success; }))));
        }

        private Composite CreateRestBehavior()
        {
            return new Decorator(ret => !StyxWoW.Me.IsFlying && !StyxWoW.Me.Combat,
                new PrioritySelector(
                    new Decorator(ret => RoutineManager.Current.RestBehavior != null,
                        RoutineManager.Current.RestBehavior),
                    new Decorator(ret => RoutineManager.Current.NeedRest,
                        new Action(ret => RoutineManager.Current.Rest()))));
        }
    }


    internal class ActionIdle : Action
    {
        protected override RunStatus Run(object context)
        {
            if (Parent is Selector || Parent is Decorator)
                return RunStatus.Success;

            return RunStatus.Failure;
        }
    }


}
