using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Bots.Grind;
using Bots.ArchaeologyBuddy;
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
using TreeSharp;
using Action = TreeSharp.Action;


namespace PoolFishingBuddy
{
    public class PoolFisher : BotBase
    {
        #region variables

        private Composite _root;
        private int _currenthotspot = -1;
        
        //private WoWPoint _checkpoint;
        static public bool looking4NewPoint;
        static public bool looking4NewPool;
        static public bool MeIsFishing;
        static public Thread MonitoringThread;
        static public Thread GetValuesThread;

        static public int Ping;
        static public int tries = 0;
        static public Stopwatch lootTimer = new Stopwatch();
        static public volatile List<ulong> PermaBlacklist = new List<ulong>();
        static public WoWGameObject Pool = null;
        static public WoWFishingBobber Bobber = null;
        static public List<WoWPoint> PoolPoints = new List<WoWPoint>(20);
        static public WoWPoint WaterSurface;

        #endregion

        static public GrindArea GrindArea { get; set; }
        static public int? GrindAreaTime { get; set; }
        static public int GrindAreaTimer { get; set; }
        static public List<WoWPoint> HotspotList;
        static public int HotspotIndex { get; set; }

        #region Overrides of BotBase

        private readonly Version _version = new Version(1, 0, 1);

        public override string Name
        {
            get { return "PoolFisher " + _version; }
        }

        public override PulseFlags PulseFlags { get { return PulseFlags.All; } }

        public override Form ConfigurationForm { get { return new FormFishConfig(); } }

        public override void Start()
        {
            if (!StyxWoW.IsLifetimeUser)
            {
                Logging.Write(System.Drawing.Color.Red, "{0} - Your don't have lifetime subscription. Stopping!", Helpers.TimeNow);
                TreeRoot.Stop();
            }

            Helpers.blacklistSchoolsFromSettings();

            ObjectManager.Update();
            List<WoWGameObject> poolList = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.SubType == WoWGameObjectType.FishingHole && !Blacklist.Contains(o.Guid) && !PoolFisher.PermaBlacklist.Contains(o.Entry) && o.Distance2D <= 100 && o.Location.X != 0).OrderBy(o => o.Distance).ToList();

            GrindArea = ProfileManager.CurrentProfile.GrindArea;
            HotspotList = GrindArea.Hotspots.ConvertAll<WoWPoint>(hs => hs.ToWoWPoint());
            HotspotIndex = 0;
            GrindAreaTime = 0;
            GrindAreaTimer = 0;

            looking4NewPoint = true;
            looking4NewPool = true;
            MeIsFishing = false;
            PoolPoints.Clear();

            Styx.BotEvents.OnBotStart += Helpers.Init;
            Styx.BotEvents.OnBotStop += Helpers.Final;
            if (TreeRoot.IsRunning)
                Helpers.Init(new System.EventArgs());


            StyxSettings.Instance.LogoutForInactivity = false;
        }

        public override void Stop()
        {
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
                                new Action(ret => Logging.Write("{0} - [INVALID] Waiting", Helpers.TimeNow)),
                                new ActionSleep(10000))),

                        new Decorator(ret => StyxWoW.Me.IsFalling,
                            new ActionSleep(1000)),

                        new Decorator(ret => StyxWoW.Me.Combat,
                            new Action(ret => Helpers.equipWeapon())),

                        LevelBot.CreateDeathBehavior(),

                        new Decorator(ret => !StyxWoW.Me.Mounted && StyxWoW.Me.Combat,
                            new Sequence(
                                new Action(ret => MeIsFishing = false),
                                new Action(ret => tries = 0),
                                LevelBot.CreateCombatBehavior())),

                        
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
                        LevelBot.CreateVendorBehavior(),

                        
                        CreateFishingBehaviour(),
                        
                        new Decorator(ret => !MeIsFishing,
                            new PrioritySelector(

                                CreateMoveToPoolBehaviour(),

                                new Decorator(ret => PoolPoints.Count == 0,
                                    new PrioritySelector(

                                        

                                        CreatePathBehaviour(),
                                        CreateLookForPoolBehaviour()
                                    
                                    ))

                            ))
                        ));
            }
        }

        #endregion

        private Composite CreateLookForPoolBehaviour()
        {
            return new Decorator(ret => Helpers.findPool && looking4NewPool && Pool.X != 0,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateLookForPoolsBehaviour", Helpers.TimeNow))
                    ));
        }

        private Composite CreateMoveToPoolBehaviour()
        {
            return new Decorator(ret => Pool != null && !Blacklist.Contains(Pool.Guid) && !PoolFisher.PermaBlacklist.Contains(Pool.Entry),
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMoveToPoolBehaviour", Helpers.TimeNow)),
                    new PrioritySelector(

                        // Get PoolPoint
                        new Decorator(ret => looking4NewPoint,
                            new Action(ret => Helpers.findPoolPoint())),

                        // Blacklist if Navigator can't generate Path
                        new Decorator(ret => PoolPoints.Count == 0,
                            new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - No path found to {1}, blacklisting for 2 minutes. (PoolFisher)", Helpers.TimeNow, Pool.Name)),
                                    new Action(ret => Helpers.BlackListPool()),
                                    new Action(delegate { return RunStatus.Success; })
                                    )),

                        // Blacklist if other Player is detected
                        new Decorator(ret => Helpers.PlayerDetected && !PoolFisherSettings.Instance.NinjaPools,
                            new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Detected another player in pool range, blacklisting for 2 minutes.", Helpers.TimeNow)),
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

                                // Dismount
                                new Decorator(ret => StyxWoW.Me.Location.Distance(PoolPoints[0]) <= 2 && StyxWoW.Me.Mounted, //&& !StyxWoW.Me.IsMoving,
                                    new Sequence(
                                        new Action(ret => Mount.Dismount()),
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Location {1}, PoolPoint: {2}", Helpers.TimeNow, StyxWoW.Me.Location, PoolPoints[0])),
                                        new Wait(3, ret => StyxWoW.Me.Mounted, new ActionIdle()),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 500))
                                        )),

                                // in Line Line of sight?
                                new Decorator(ret => StyxWoW.Me.Location.Distance(PoolPoints[0]) <= 2 && !Pool.InLineOfSight && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Mounted,
                                    new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Pool is not in Line of Sight!", Helpers.TimeNow)),
                                    new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                                    new Action(ret => PoolPoints.Sort((p1, p2) => p1.Z.CompareTo(p2.Z))),
                                    new Action(ret => PoolPoints.Reverse()),
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - New PoolPoint: {1}, Distance: {2}", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location.Distance(PoolPoints[0])))
                                )),

                                // swimming?
                                new Decorator(ret => StyxWoW.Me.Location.Distance2D(PoolPoints[0]) <= 10 && StyxWoW.Me.IsSwimming && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Mounted,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Swimming at {1}...", Helpers.TimeNow, StyxWoW.Me.Location)),
                                        new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                                        new Action(ret => PoolPoints.Sort((p1, p2) => p1.Z.CompareTo(p2.Z))),
                                        //new Action(ret => PoolPoints.Reverse()),
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - New PoolPoint: {1}, Distance: {2}", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location.Distance(PoolPoints[0])))
                                        )),

                                // Move without Mount
                                new Decorator(ret => StyxWoW.Me.Location.Distance(PoolPoints[0]) > 2 && StyxWoW.Me.Location.Distance2D(PoolPoints[0]) < 40 && !StyxWoW.Me.Mounted && (/*Navigator.MoveTo(PoolPoints[0]) != MoveResult.PathGenerationFailed ||*/ Navigator.CanNavigateFully(StyxWoW.Me.Location, PoolPoints[0])),
                                    new Sequence(
                                        new ActionSetActivity(ret => "Moving to PoolPoint: " + PoolPoints[0] + ", distance: " + PoolPoints[0].Distance(StyxWoW.Me.Location) + ", Location: " + StyxWoW.Me.Location + "(Not Mounted)"),
                                        new Action(ret => Logging.Write("{0} - Moving to PoolPoint: {1}, distance: {2}, Location: {3}. (Not Mounted)", Helpers.TimeNow, PoolPoints[0], PoolPoints[0].Distance(StyxWoW.Me.Location), StyxWoW.Me.Location)),
                                        new Action(ret => Navigator.MoveTo(PoolPoints[0])),
                                        new Wait(10, ret => StyxWoW.Me.IsMoving, new ActionIdle())
                                )),

                                // Move with Mount
                                new Decorator(ret => StyxWoW.Me.Location.Distance(PoolPoints[0]) > 2 /*&& (Navigator.MoveTo(PoolPoints[0]) == Styx.Logic.Pathing.MoveResult.PathGenerationFailed || !Navigator.CanNavigateFully(StyxWoW.Me.Location, PoolPoints[0]))*/ || StyxWoW.Me.Mounted,
                                    new Sequence(
                                        new ActionSetActivity(ret => "Moving to PoolPoint: " + PoolPoints[0] + ", distance: " + PoolPoints[0].Distance(StyxWoW.Me.Location) + ", Location: " + StyxWoW.Me.Location + "(Mounted)"),
                                        new Action(ret => Logging.Write("{0} - Moving to PoolPoint: {1}, distance: {2}, Location: {3}. (Mounted)", Helpers.TimeNow, PoolPoints[0], PoolPoints[0].Distance(StyxWoW.Me.Location), StyxWoW.Me.Location)),
                                        new PrioritySelector(
                                            CreateMountBehaviour(),
                                            new Action(ret => Flightor.MoveWithTrace(PoolPoints[0])))
                                ))
                        ))
                )));
        }

        private Composite CreateFishingBehaviour()
        {
            return new Decorator(ret => PoolPoints.Count > 0 && !looking4NewPool && StyxWoW.Me.Location.Distance2D(PoolPoints[0]) < 10 && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsSwimming && !StyxWoW.Me.Combat,// && Pool.InLineOfSight,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateFishingBehaviour", Helpers.TimeNow)),
                    new Action(ret => MeIsFishing = true),
                    new PrioritySelector(

                        // Wait for GCD
                        new Decorator(ret => StyxWoW.GlobalCooldown,
                            new ActionIdle()),

                        // Pool still there?
                        new Decorator(ret => !Helpers.PoolIsStillThere,
                            new Sequence(
                                new Action(ret => Logging.Write("{0} - Fishing Pool is gone, moving on.", Helpers.TimeNow)),
                                new Action(ret => Helpers.equipWeapon())
                                )),

                        // reached max tries for casting?
                        new Decorator(ret => tries == 10,
                            new Sequence(
                            new Action(ret => Logging.Write("{0} - Tried to cast 10 times. CreateMoveToPoolBehaviour!", Helpers.TimeNow)),
                            new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                            new Action(ret => PoolPoints.Sort((p1, p2) => p1.Z.CompareTo(p2.Z))),
                            //new Action(ret => PoolPoints.Reverse()),
                            new Action(ret => MeIsFishing = false),
                            new Action(ret => tries = 0),
                            CreateMoveToPoolBehaviour()
                            )),

                        // in Line Line of sight?
                        new Decorator(ret => !Pool.InLineOfSight,
                            new Sequence(
                                new Action(ret => Logging.Write("{0} - Pool is not in Line of Sight. CreateMoveToPoolBehaviour!", Helpers.TimeNow)),
                                new Action(ret => MeIsFishing = false),
                                CreateMoveToPoolBehaviour()
                        )),

                        // swimming?
                        new Decorator(ret => StyxWoW.Me.IsSwimming,
                            new Sequence(
                                new Action(ret => Logging.Write("{0} - Me is swimming. CreateMoveToPoolBehaviour!", Helpers.TimeNow)),
                                new Action(ret => MeIsFishing = false),
                                CreateMoveToPoolBehaviour()
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
                                            tries = 0;
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
                                    tries = 0;
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
                                    tries = 0;
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
                        new Decorator(ret => Helpers.IsFishing && !Helpers.BobberIsInTheHole && Helpers.PoolIsStillThere && tries != 20,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Missed the pool!", Helpers.TimeNow)),
                                new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                new Action(ret => Thread.Sleep((Ping * 2) + 200)),
                                new Sequence(
                                    // Luring
                                    new Decorator(ret => PoolFisherSettings.Instance.useLure,
                                        new Action(Ret => Helpers.applylure()))),
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Try: {1} of 10.", Helpers.TimeNow, tries)),
                                new Action(ret => TreeRoot.StatusText = "Cast Fishing."),
                                new Action(ret => SpellManager.Cast("Fishing")),
                                new Action(ret => tries++),
                                new Wait(5, ret => StyxWoW.Me.IsCasting, new ActionIdle()),
                                new Action(ret => Thread.Sleep((Ping * 2) + 500))
                                )),

                        // Poolfishing
                        new Decorator(ret => !Helpers.IsFishing && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsSwimming && Helpers.PoolIsStillThere,
                            new PrioritySelector(

                                // equip pole
                                new Decorator(ret => !Helpers.equipPole,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Could not find any fishing Poles!", Helpers.TimeNow)),
                                        new Action(ret => TreeRoot.Stop()))),

                                // cast fishing
                                new Sequence(
                                    new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                    new Action(ret => Thread.Sleep((Ping * 2) + 200)),
                                    new Sequence(
                                        // Luring
                                        new Decorator(ret => PoolFisherSettings.Instance.useLure,
                                            new Action(Ret => Helpers.applylure()))),
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Try: {1} of 10.", Helpers.TimeNow, tries)),
                                    new Action(ret => TreeRoot.StatusText = "Cast Fishing"),
                                    new Action(ret => SpellManager.Cast("Fishing")),
                                    new Action(ret => tries++),
                                    new Wait(5, ret => StyxWoW.Me.IsCasting, new ActionIdle())
                                    ))),

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


        private Composite CreatePathBehaviour()
        {
            return new PrioritySelector(

                new Decorator(ret => HotspotList.Count <= 0,
                    new Sequence(
                        new Action(ret => Logging.Write("{0} - Profile has no hotspots!", Helpers.TimeNow)),
                        new Action(ret => TreeRoot.Stop()))),

                new Decorator(ret => _currenthotspot < 0,
                    new Action(ret => _currenthotspot = HotspotList.IndexOf(HotspotList.OrderBy(hs => StyxWoW.Me.Location.Distance(hs)).FirstOrDefault()))),

                new Decorator(ret => _currenthotspot >= HotspotList.Count,
                    new Action(ret => _currenthotspot = 0)),

                new Decorator(ret => StyxWoW.Me.Location.Distance(HotspotList[_currenthotspot]) < 20,
                    new Action(ret => _currenthotspot++)),

                new DecoratorSetContext<WoWPoint>(() => HotspotList[_currenthotspot], hotspot => true,
                    new Sequence(
                        //new Action(ret => Logging.Write("Moving to hotspot: {0}", ret)),
                        new ActionSetActivity(hotspot => "Moving to hotspot: " + (WoWPoint)hotspot + ", distance: " + ((WoWPoint)hotspot).Distance(StyxWoW.Me.Location)),
                        CreateMoveBehaviour())));
        }

        private Composite CreateMoveBehaviour()
        {
            return new Sequence(
                new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMoveBehaviour", Helpers.TimeNow)),

                new PrioritySelector(

                    new Decorator(ret => !StyxWoW.Me.Mounted,
                        CreateMountBehaviour()),
                    new ActionMove()));
        }

        private Composite CreateMountBehaviour()
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
                                new Wait(2, ret => StyxWoW.Me.IsCasting, new ActionIdle()),
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
                                new Action(ret => Logging.Write("{0} - Mounting {1}.", Helpers.TimeNow, WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name)),
                                    new ActionSetActivity(spell => "Casting " + WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name),
                                    new Action(spell => WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Cast()),
                                    new Wait(2, ret => StyxWoW.Me.IsCasting, new ActionIdle()),
                                    new Action(ret => Thread.Sleep(2000 + (Ping * 3))),
                                    new Action(ret => StyxWoW.SleepForLagDuration())
                                        )),

                        new Decorator(ret => PoolFisherSettings.Instance.FlyingMountID == 0,
                        new Sequence(
                            new Action(ret => Logging.Write("{0} - No mount set. Mounting first known: {1}.", Helpers.TimeNow, PoolFisherSettings.Instance.FlyingMountID)),
                            new DecoratorSetContext<WoWSpell>(() => (StyxWoW.Me.ZoneId == 5146 ? MountHelper.UnderwaterMounts : (StyxWoW.IsLifetimeUser ? MountHelper.FlyingMounts : MountHelper.GroundMounts)).Where(m => Helpers.IsUsableSpell(m.CreatureSpellId)).FirstOrDefault().CreatureSpell, mount => mount != null,
                                CreateCastBehaviour())))
                                
                        )));
        }

        private Composite CreateCastBehaviour()
        {
            return new Sequence(
                new Action(ret => Logging.WriteDebug("{0} - Composit: CreateCastBehaviour", Helpers.TimeNow)),
                CreateMoveStopBehaviour(),
                new ActionSetActivity(spell => "Casting " + ((WoWSpell)spell).Name),
                new Action(spell => ((WoWSpell)spell).Cast()),
                new Wait(2, ret => StyxWoW.Me.IsCasting, new ActionIdle()),
                new Action(ret => StyxWoW.SleepForLagDuration()));
        }

        private Composite CreateMoveStopBehaviour()
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
