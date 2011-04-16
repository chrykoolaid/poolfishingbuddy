using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Styx;
using Styx.Helpers;

using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Inventory.Frames.LootFrame;

using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Styx.Logic.Inventory.Frames.Taxi;

namespace PoolFishingBuddy
{
    class Training
    {
        public struct Trainer
        {
            public uint t_ID;
            public uint t_Map;
            public WoWPoint t_p;
            public WoWPoint t_d;

            public Trainer(uint ID, uint Map, WoWPoint Location, WoWPoint Destination)
            {
                this.t_ID = ID;
                this.t_Map = Map;
                this.t_p = Location;
                this.t_d = Destination;
            }

            public uint ID { get { return this.t_ID; } }
            public uint Map { get { return this.t_Map; } }
            public WoWPoint Location { get { return this.t_p; } }
            public WoWPoint Destination { get { return this.t_d; } }
        }

        static public List<Trainer> AllianceTrainers = new List<Trainer>
        {
            // Eastern Kingdom
            new Trainer(2834, 0, new WoWPoint(-14449.2, 468.424, 15.4565), new WoWPoint(-14449.2, 468.424, 15.4565)), // tested: Booty Bay
            //new Trainer(5161, 0, new WoWPoint(-4605.72, -1093.62, 511.831), new WoWPoint(-5036.389, -814.8766, 495.1284)), // disabled: Ironforge
            new Trainer(1683, 0, new WoWPoint(-5218.556, -3115.246, 300.3802), new WoWPoint(-5218.556, -3115.246, 300.3802)), // tested: Loch Modan
            new Trainer(1700, 0, new WoWPoint(-5197.73, 54.8182, 385.852), new WoWPoint(-5197.73, 54.8182, 385.852)), // tested: Dun Morogh
            new Trainer(1680, 0, new WoWPoint(-9309.182, -2146.308, 63.48353), new WoWPoint(-9309.182, -2146.308, 63.48353)), // tested: Lakeshire
            new Trainer(1651, 0, new WoWPoint(-9381.473, -113.5128, 58.8209), new WoWPoint(-9378.715, -104.6187, 60.65849)), // tested: Elwynn Forest
            new Trainer(3179, 0, new WoWPoint(-3763.51, -723.533, 2.004127), new WoWPoint(-3763.51, -723.533, 2.004127)), // tested: Menethil Harbor
            new Trainer(5493, 0, new WoWPoint(-8801.298, 768.9528, 96.33826), new WoWPoint(-8793.525, 762.0019, 96.9619)), // tested: Stormwind
            //new Trainer(50570, --
            // Kalimdor
            new Trainer(3607, 1, new WoWPoint(8325.846, 1064.042, 10.54009), new WoWPoint(7459.9, -326.56, 8.089962)), // tested: Rut'theran
            //new Trainer(12032, 1, new WoWPoint(-1719.02, 3212.89, 4.48054), new WoWPoint(-1719.02, 3212.89, 4.48054)), // tested: Desolace
            //new Trainer(7946, 1, new WoWPoint(-4288.966, 2295.356, 9.409933), new WoWPoint(-4288.966, 2295.356, 9.409933)), // tested: Feralas
            //new Trainer(4156, 1, new WoWPoint(8325.378, 1062.652, 10.7661), new WoWPoint(8325.378, 1062.652, 10.7661)), // disabled: Darnassus (no pools in that area..)
            // Outlands
            //new HordeTrainer(18911, 530, new WoWPoint(-279.86, 5551.56, 22.6139), new WoWPoint(-279.86, 5551.56, 22.6139)), // tested: Cenarion Refuge
            new Trainer(17101, 530, new WoWPoint(-4266.34, -12985.4, 2.35574), new WoWPoint(-4266.34, -12985.4, 2.35574)),
            new Trainer(16774, 530, new WoWPoint(-3712.96, -11404, -133.765), new WoWPoint(-3712.96, -11404, -133.765)),
            // Northrend
            //new Trainer(28742, 571, new WoWPoint(5707.31, 612.214, 646.891), new WoWPoint(5731.364, 1015.048, 174.4803)), // tested: Dalaran
            new Trainer(26909, 571, new WoWPoint(533.751, -5048.23, 10.4191), new WoWPoint(533.751, -5048.23, 10.4191)),
            new Trainer(26993, 571, new WoWPoint(2144.24, 5234.32, 19.6292), new WoWPoint(2144.24, 5234.32, 19.6292)),
        };

        static public List<Trainer> HordeTrainers = new List<Trainer>
        {
            new Trainer(44975, 1, new WoWPoint(1706.507, -4115.052, 48.08058), new WoWPoint(1706.507, -4115.052, 48.08058)), // tested: Orgrimmar
            new Trainer(3332, 1, new WoWPoint(2000.62, -4659.66, 26.8223), new WoWPoint(2000.62, -4659.66, 26.8223)), // tested: Orgrimmar
            new Trainer(3028, 1, new WoWPoint(-1172.76, -69.2332, 162.384), new WoWPoint(-1172.76, -69.2332, 162.384)), // tested: Thunderbluff
            new Trainer(5938, 1, new WoWPoint(-2350.51, -238.716, -8.20635), new WoWPoint(-2350.51, -238.716, -8.20635)), // tested: Mulgore
            new Trainer(12961, 1, new WoWPoint(3375.959, 1072.715, 1.883213), new WoWPoint(3375.959, 1072.715, 1.883213)), // tested: Ashenvale
            new Trainer(12032, 1, new WoWPoint(-1719.02, 3212.89, 4.48054), new WoWPoint(-1719.02, 3212.89, 4.48054)), // tested: Desolace
            new Trainer(5941, 1, new WoWPoint(-910.2767, -4990.892, 12.14395), new WoWPoint(-900.3952, -4969.15, 14.9322)), // tested: Durotar
            //new HordeTrainer(49885, 1, new WoWPoint(3515.285, -6516.967, 56.07669), new WoWPoint(3515.285, -6516.967, 56.07669)), disabled: Azshara (All in one trainer, to much gossip context..)
            //new HordeTrainer(45286, -- ), disabled: The Lost Isles (3 phases with different locations. Goblin only..)
            // Eastern Kingdom
            //new HordeTrainer(16780, 530, new WoWPoint(9607.08, -7324.33, 15.1517), new WoWPoint(9607.08, -7324.33, 15.1517)), // disabled: Silvermoon (Flying not possible im Burning Crusade areas of Old Azeroth.)
            //new HordeTrainer(4573, 0, new WoWPoint(1678.076, 95.66591, -62.11353), new WoWPoint(1659.394, 732.289, 79.9381)), // disabled: Undercity (Brightwatersee is near there.)
            new Trainer(14740, 0, new WoWPoint(-626.071, -4667.71, 6.60807), new WoWPoint(-626.071, -4667.71, 6.60807)), // tested: Revantusk Village
            new Trainer(5690, 0, new WoWPoint(2301.52, -1.88889, 23.358), new WoWPoint(2301.52, -1.88889, 23.358)), // tested: Brightwatersee
            new Trainer(2834, 0, new WoWPoint(-14449.2, 468.424, 15.4565), new WoWPoint(-14449.2, 468.424, 15.4565)), // tested: Booty Bay
            // Outlands
            new Trainer(18911, 530, new WoWPoint(-279.86, 5551.56, 22.6139), new WoWPoint(-279.86, 5551.56, 22.6139)), // tested: Cenarion Refuge
            new Trainer(18018, 530, new WoWPoint(272.576, 7853.82, 24.1883), new WoWPoint(272.576, 7853.82, 24.1883)), // tested: Zabra'Jin
            // Northrend
            new Trainer(28742, 571, new WoWPoint(5707.31, 612.214, 646.891), new WoWPoint(5731.364, 1015.048, 174.4803)), // tested: Dalaran
            new Trainer(32474, 571, new WoWPoint(2804.14, 6165.57, 85.4861), new WoWPoint(2742.906, 6097.921, 77.06467)), // tested: Warsong Hold
            new Trainer(26957, 571, new WoWPoint(2024.24, -6207.31, 8.08216), new WoWPoint(2024.24, -6207.31, 8.08216)), // tested: Vengeance Landing
        };

        public static uint MaxSkillLevel { get { return 525; } }

        public static bool gotDalaranTeleport
        {
            get
            {
                ObjectManager.Update();
                List<WoWGameObject> ObjectList = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.Distance2D <= 150).OrderBy(o => o.Distance).ToList();
                foreach (WoWGameObject o in ObjectList)
                {
                    if (o.Entry == 191230)
                    {
                        PoolFisher.DalaraTeleport = o;
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Flightpath from Lor'danel to Rut'theran
        /// </summary>
        public static void useRuttheranFlightPath()
        {
            ObjectManager.Update();
            List<WoWUnit> WoWUnitList = ObjectManager.GetObjectsOfType<WoWUnit>();
            foreach (WoWUnit u in WoWUnitList)
            {
                if (u.Entry == 3841)
                {
                    Logging.Write("{0} - Interact with {1}", Helpers.TimeNow, u.Name);
                    u.Target();
                    Thread.Sleep(2000);
                    u.Interact();
                    Thread.Sleep(2000);
                    try
                    {
                        if (TaxiFrame.Instance.IsVisible)
                        {
                            foreach (TaxiFrame.TaxiFrameNode n in TaxiFrame.Instance.Nodes)
                            {
                                //Logging.Write("Name: {0}, Slot: {1}, Reachable: {2}.", n.Name, n.Slot, n.Reachable);
                                if (n.Slot == 5 && n.Reachable)
                                {
                                    n.TakeNode();
                                }
                            }
                        }
                    }
                    catch (Exception e) { Logging.Write(e.ToString()); }
                }
            }
        }

        /// <summary>
        /// Flightpath from Rut'theran to Lor'danel
        /// </summary>
        public static void useLordanelFlightPath()
        {
            while (StyxWoW.Me.Location.Distance(new WoWPoint(8383.152, 983.699, 30.76294)) > 2)
            {
                Navigator.MoveTo(new WoWPoint(8383.152, 983.699, 30.76294));
                Thread.Sleep(100);
            }

            ObjectManager.Update();
            List<WoWUnit> WoWUnitList = ObjectManager.GetObjectsOfType<WoWUnit>();
            foreach (WoWUnit u in WoWUnitList)
            {
                if (u.Entry == 3838)
                {
                    Logging.Write("{0} - Interact with {1}", Helpers.TimeNow, u.Name);
                    u.Target();
                    Thread.Sleep(2000);
                    u.Interact();
                    Thread.Sleep(2000);
                    try
                    {
                        if (TaxiFrame.Instance.IsVisible)
                        {
                            foreach (TaxiFrame.TaxiFrameNode n in TaxiFrame.Instance.Nodes)
                            {
                                //Logging.Write("Name: {0}, Slot: {1}, Reachable: {2}.", n.Name, n.Slot, n.Reachable);
                                if (n.Slot == 4 && n.Reachable)
                                {
                                    n.TakeNode();
                                }
                            }
                        }
                    }
                    catch (Exception e) { Logging.Write(e.ToString()); }
                }
            }
        }

        public static bool needTraining(WoWSkill s)
        {
            ulong money = ObjectManager.Me.Copper;
            s = ObjectManager.Me.GetSkill(SkillLine.Fishing);
            if (s.CurrentValue == 0 && money < 95 || ObjectManager.Me.Level < 5) return false;
            if (s.CurrentValue == 75 && money < 475 || ObjectManager.Me.Level < 10) return false;
            if (s.CurrentValue == 150 && money < 9500) return false;
            if (s.CurrentValue == 225 && money < 23750) return false;
            if (s.CurrentValue == 300 && money < 95000) return false;
            if (s.CurrentValue == 375 && money < 332500) return false;
            return (s.MaxValue == s.CurrentValue) && s.CurrentValue < MaxSkillLevel;
        }

        public static void getTrainer()
        {
            if (StyxWoW.Me.IsHorde)
            {
                List<Trainer> trainerList = HordeTrainers.Where(t => t.Map == StyxWoW.Me.MapId).OrderBy(t => StyxWoW.Me.Location.Distance(t.Location)).ToList();

                if (trainerList.Count > 0)
                {
                    PoolFisher.TrainerDestination = trainerList[0].Destination;
                    PoolFisher.TrainerLocation = trainerList[0].Location;
                    PoolFisher.TrainerID = trainerList[0].ID;
                }
                else
                {
                    Logging.Write(System.Drawing.Color.Red, "{0} - No valid Trainer. Moving on without training.", Helpers.TimeNow);
                    PoolFisherSettings.Instance.Load();
                    PoolFisherSettings.Instance.TrainingEnabled = false;
                    PoolFisherSettings.Instance.Save();
                }
            }
            if (StyxWoW.Me.IsAlliance)
            {
                List<Trainer> trainerList = AllianceTrainers.Where(t => t.Map == StyxWoW.Me.MapId).OrderBy(t => StyxWoW.Me.Location.Distance(t.Location)).ToList();

                if (trainerList.Count > 0)
                {
                    PoolFisher.TrainerDestination = trainerList[0].Destination;
                    PoolFisher.TrainerLocation = trainerList[0].Location;
                    PoolFisher.TrainerID = trainerList[0].ID;
                }
                else
                {
                    Logging.Write(System.Drawing.Color.Red, "{0} - No valid Trainer. Moving on without training.", Helpers.TimeNow);
                    PoolFisherSettings.Instance.Load();
                    PoolFisherSettings.Instance.TrainingEnabled = false;
                    PoolFisherSettings.Instance.Save();
                }
            }
        }

        public static void InteractWithTrainer()
        {
            if (StyxWoW.Me.IsHorde)
            {
                List<Trainer> trainerList = HordeTrainers.Where(t => t.Map == StyxWoW.Me.MapId).OrderBy(t => StyxWoW.Me.Location.Distance(t.Location)).ToList();
                ObjectManager.Update();
                List<WoWUnit> WoWUnitList = ObjectManager.GetObjectsOfType<WoWUnit>();
                foreach (WoWUnit u in WoWUnitList)
                {
                    if (u.Entry == trainerList[0].ID)
                    {
                        PoolFisher.Trainer = u;
                        Logging.Write("Name: {0}, Distance: {1}.", PoolFisher.Trainer.Name, StyxWoW.Me.Location.Distance(PoolFisher.Trainer.Location));
                    }
                }
            }

            if (StyxWoW.Me.IsAlliance)
            {
                List<Trainer> trainerList = AllianceTrainers.Where(t => t.Map == StyxWoW.Me.MapId).OrderBy(t => StyxWoW.Me.Location.Distance(t.Location)).ToList();
                ObjectManager.Update();
                List<WoWUnit> WoWUnitList = ObjectManager.GetObjectsOfType<WoWUnit>();
                foreach (WoWUnit u in WoWUnitList)
                {
                    if (u.Entry == trainerList[0].ID)
                    {
                        PoolFisher.Trainer = u;
                        Logging.Write("Name: {0}, Distance: {1}.", PoolFisher.Trainer.Name, StyxWoW.Me.Location.Distance(PoolFisher.Trainer.Location));
                    }
                }
            }

            if (PoolFisher.Trainer != null && StyxWoW.Me.Location.Distance(PoolFisher.Trainer.Location) <= 2)
            {
                Logging.Write("{0} - Interact with {1}", Helpers.TimeNow, PoolFisher.Trainer.Name);
                PoolFisher.Trainer.Target();
                Thread.Sleep(2000);
                PoolFisher.Trainer.Interact();
                Thread.Sleep(2000);
                try
                {
                    if (GossipFrame.Instance.IsVisible)
                    {
                        foreach (var option in GossipFrame.Instance.GossipOptionEntries)
                        {
                            if (option.Type == GossipEntry.GossipEntryType.Trainer)
                            {
                                GossipFrame.Instance.SelectGossipOption(option.Index);
                                break;
                            }
                        }
                    }
                }
                catch (Exception e) { Logging.Write(e.ToString()); }
                Thread.Sleep(2000);
                TreeRoot.StatusText = "Training";
                Lua.DoString("BuyTrainerService(0)");
                Thread.Sleep(5000);
                Lua.DoString("CloseTrainer()");

                // for testing
                PoolFisher.need2Train = false;
                PoolFisher.back2Path = true;
            }
            else if (PoolFisher.Trainer != null)
            {
                PoolFisher.FlyToTrainer = false;
                Logging.Write(System.Drawing.Color.Red, "{0} - Trainer not in range to interact. Moving closer.", Helpers.TimeNow);
                PoolFisher.TrainerLocation = PoolFisher.Trainer.Location;
            }
            else
            {
                Logging.Write(System.Drawing.Color.Red, "{0} - Trainer is dead or moved away. Moving on without training.", Helpers.TimeNow);
                PoolFisherSettings.Instance.Load();
                PoolFisherSettings.Instance.TrainingEnabled = false;
                PoolFisherSettings.Instance.Save();

                // for testing
                PoolFisher.need2Train = false;
                PoolFisher.back2Path = true;
            }
        }
    }
}
