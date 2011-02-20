using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

using Styx;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Bots.Grind;
using Styx.Logic.POI;


namespace PoolFishingBuddy.Forms
{
    public partial class FormFishConfig : Form
    {
        public FormFishConfig()
        {
            InitializeComponent();
        }

        private void FormFishConfig_Load(object sender, EventArgs e)
        {
            PoolFisherSettings.Instance.Load();
            ProtectedItemsManager.ReloadProtectedItems();
            //Logging.Write(System.Drawing.Color.Red, "{0} - ProtectedItems count: {1}", Helpers.TimeNow, ProtectedItemsManager.GetAllItemIds().Count);

            try
            {
                pictureBox1.Image = Image.FromStream(new MemoryStream(new WebClient().DownloadData("http://poolfishingbuddy.googlecode.com/svn/images/fish.png")));
                Icon = new Icon(new MemoryStream(new WebClient().DownloadData("http://poolfishingbuddy.googlecode.com/svn/images/fish.ico")), 32, 32);
            }
            catch (InvalidCastException ex)
            {
                Logging.Write(System.Drawing.Color.Red, "{0} - Exception: {1}", Helpers.TimeNow, ex);
            }

            checkNinjaPools.Checked = PoolFisherSettings.Instance.NinjaPools;
            MinCastRangeText.Text = PoolFisherSettings.Instance.MinCastRange.ToString();
            MaxCastRangeText.Text = PoolFisherSettings.Instance.MaxCastRange.ToString();
            MaxCastAttemptsText.Text = PoolFisherSettings.Instance.MaxCastAttempts.ToString();
            MaxNewLocAttemptsText.Text = PoolFisherSettings.Instance.MaxNewLocAttempts.ToString();
            numericHeightMod.Value = PoolFisherSettings.Instance.HeightModifier;

            #region Bags full

            if (PoolFisherSettings.Instance.ShouldMail == false)
            {
                radioHearthAndExit.Checked = true;
                radioMailTo.Checked = false;
                textMailTo.Text = "";
                textMailTo.Enabled = false;
            }
            else
            {
                radioHearthAndExit.Checked = false;
                radioMailTo.Checked = true;
                textMailTo.Enabled = true;
                textMailTo.Text = PoolFisherSettings.Instance.MailRecipient;
            }

            #endregion

            #region Blacklist

            checkBlacklistSchools.Checked = PoolFisherSettings.Instance.BlacklistSchools;
            tabControlBlacklist.Visible = PoolFisherSettings.Instance.BlacklistSchools;

            if (PoolFisherSettings.Instance.BlacklistSchools)
            {
                /// Cataclysm
                checkBLAlbinoCavefish.Checked       = PoolFisherSettings.Instance.BLAlbinoCavefish;
                checkBLAlgaefinRockfish.Checked     = PoolFisherSettings.Instance.BLAlgaefinRockfish;
                checkBLBlackbellyMudfish.Checked    = PoolFisherSettings.Instance.BLBlackbellyMudfish;
                checkBLFathomEel.Checked            = PoolFisherSettings.Instance.BLFathomEel;
                checkBLHighlandGuppy.Checked        = PoolFisherSettings.Instance.BLHighlandGuppy;
                checkBLMountainTrout.Checked        = PoolFisherSettings.Instance.BLMountainTrout;
                checkBLPoolofFire.Checked           = PoolFisherSettings.Instance.BLPoolofFire;
                checkBLShipwreckDebris.Checked      = PoolFisherSettings.Instance.BLShipwreckDebris;
                /// Wrath of the Lich King
                checkBLBoreanManOWar.Checked        = PoolFisherSettings.Instance.BLBoreanManOWar;
                checkBLDeepSeaMonsterbelly.Checked  = PoolFisherSettings.Instance.BLDeepSeaMonsterbelly;
                checkBLDragonfinAngelfish.Checked   = PoolFisherSettings.Instance.BLDragonfinAngelfish;
                checkBLFangtoothHerring.Checked     = PoolFisherSettings.Instance.BLFangtoothHerring;
                checkBLGlacialSalmon.Checked        = PoolFisherSettings.Instance.BLGlacialSalmon;
                checkBLGlassfinMinnow.Checked       = PoolFisherSettings.Instance.BLGlassfinMinnow;
                checkBLImperialMantaRay.Checked     = PoolFisherSettings.Instance.BLImperialMantaRay;
                checkBLMoonglowCuttlefish.Checked   = PoolFisherSettings.Instance.BLMoonglowCuttlefish;
                checkBLMusselbackSculpin.Checked    = PoolFisherSettings.Instance.BLMusselbackSculpin;
                checkBLNettlefish.Checked           = PoolFisherSettings.Instance.BLNettlefish;
                /// Burning Crusade 
                checkBLBluefish.Checked         = PoolFisherSettings.Instance.BLBluefish;
                checkBLBrackishMix.Checked      = PoolFisherSettings.Instance.BLBrackishMix;
                checkBLHighlandMix.Checked      = PoolFisherSettings.Instance.BLHighlandMix;
                checkBLMudfish.Checked          = PoolFisherSettings.Instance.BLMudfish;
                checkBLPureWater.Checked        = PoolFisherSettings.Instance.BLPureWater;
                checkBLDarter.Checked           = PoolFisherSettings.Instance.BLDarter;
                checkBLSporefish.Checked        = PoolFisherSettings.Instance.BLSporefish;
                checkBLSteamPumpFlotsam.Checked = PoolFisherSettings.Instance.BLSteamPumpFlotsam;
            }
            else
            {
                /// Cataclysm
                checkBLAlbinoCavefish.Checked       = false;
                checkBLAlgaefinRockfish.Checked     = false;
                checkBLBlackbellyMudfish.Checked    = false;
                checkBLFathomEel.Checked            = false;
                checkBLHighlandGuppy.Checked        = false;
                checkBLMountainTrout.Checked        = false;
                checkBLPoolofFire.Checked           = false;
                checkBLShipwreckDebris.Checked      = false;
                /// Wrath of the Lich King
                checkBLBoreanManOWar.Checked        = false;
                checkBLDeepSeaMonsterbelly.Checked  = false;
                checkBLDragonfinAngelfish.Checked   = false;
                checkBLFangtoothHerring.Checked     = false;
                checkBLGlacialSalmon.Checked        = false;
                checkBLGlassfinMinnow.Checked       = false;
                checkBLImperialMantaRay.Checked     = false;
                checkBLMoonglowCuttlefish.Checked   = false;
                checkBLMusselbackSculpin.Checked    = false;
                checkBLNettlefish.Checked           = false;
                /// Burning Crusade 
                checkBLBluefish.Checked         = false;
                checkBLBrackishMix.Checked      = false;
                checkBLHighlandMix.Checked      = false;
                checkBLMudfish.Checked          = false;
                checkBLPureWater.Checked        = false;
                checkBLDarter.Checked           = false;
                checkBLSporefish.Checked        = false;
                checkBLSteamPumpFlotsam.Checked = false;
            }

            #endregion

            #region Lures

            comboLures.DataSource = Helpers.Lures;
            comboLures.ValueMember = "Id";
            comboLures.DisplayMember = "Name";

            if (PoolFisherSettings.Instance.useLure && StyxWoW.IsInGame && StyxWoW.Me.IsValid)
            {
                comboLures.Enabled = true;
                checkUseLure.Checked = true;

                uint lureID = (uint)PoolFisherSettings.Instance.LureID;

                for (int i = 0; i < comboLures.Items.Count; i++)
                {
                    var entry = comboLures.Items[i];
                    if (entry is Helpers.LureType)
                    {
                        var lure = (Helpers.LureType)entry;

                        if (lure.ID == lureID)
                        {
                            comboLures.SelectedIndex = i;
                        }
                    }
                    else
                    {
                        Logging.Write(System.Drawing.Color.Red, "{0} - Could not find lure from last settings, luring disabled!", Helpers.TimeNow);
                        comboLures.SelectedItem = null;
                        comboLures.Enabled = false;
                        checkUseLure.Checked = false;
                    }
                }
            }
            else
            {
                comboLures.SelectedItem = null;
                comboLures.Enabled = false;
                checkUseLure.Checked = false;
            }

            #endregion

            #region Mounts

            comboMounts.Items.Clear();

            WoWSpell SwiftFlightForm = WoWSpell.FromId(40120);
            WoWSpell FlightForm = WoWSpell.FromId(33943);

            if (SpellManager.HasSpell(SwiftFlightForm))
            {
                comboMounts.Items.Add(SwiftFlightForm.Name);
                if (PoolFisherSettings.Instance.FlyingMountID == SwiftFlightForm.Id)
                {
                    comboMounts.SelectedText = SwiftFlightForm.Name;
                }
            }
            if (SpellManager.HasSpell(FlightForm))
            {
                comboMounts.Items.Add(FlightForm.Name);
                if (PoolFisherSettings.Instance.FlyingMountID == FlightForm.Id)
                {
                    comboMounts.SelectedText = FlightForm.Name;
                }
            }
            foreach (MountHelper.MountWrapper flyingMount in MountHelper.FlyingMounts)
            {
                comboMounts.Items.Add(flyingMount.Name);
                if (PoolFisherSettings.Instance.FlyingMountID == flyingMount.CreatureSpellId)
                {
                    comboMounts.SelectedItem = flyingMount.Name;
                }
            }

            #endregion

            #region Mode

            if (PoolFisherSettings.Instance.BounceMode)
            {
                comboMode.SelectedIndex = 1;
            }
            else
            {
                comboMode.SelectedIndex = 0;
            }

            #endregion

            #region Pole and Weapons

            PoolFisher.BagItems = StyxWoW.Me.BagItems;

            if (PoolFisherSettings.Instance.FishingPole != 0)
            {
                foreach (WoWItem i in PoolFisher.BagItems)
                {
                    if (i.Entry == PoolFisherSettings.Instance.FishingPole)
                    {
                        PoolFisher.poleList.Add(i);
                        comboPole.Items.Add(i.Name);
                        comboPole.SelectedItem = i.Name;
                    }
                }

                if (StyxWoW.Me.Inventory.Equipped.MainHand != null && StyxWoW.Me.Inventory.Equipped.MainHand.Entry == PoolFisherSettings.Instance.FishingPole)
                {
                    PoolFisher.poleList.Add(StyxWoW.Me.Inventory.Equipped.MainHand);
                    comboPole.Items.Add(StyxWoW.Me.Inventory.Equipped.MainHand.Name);
                    comboPole.SelectedItem = StyxWoW.Me.Inventory.Equipped.MainHand.Name;
                }
            }

            if (PoolFisherSettings.Instance.Mainhand != 0)
            {
                foreach (WoWItem i in PoolFisher.BagItems)
                {
                    if (i.Entry == PoolFisherSettings.Instance.Mainhand)
                    {
                        PoolFisher.mainhandList.Add(i);
                        comboMainhand.Items.Add(i.Name);
                        comboMainhand.SelectedItem = i.Name;
                    }
                }

                if (StyxWoW.Me.Inventory.Equipped.MainHand != null && StyxWoW.Me.Inventory.Equipped.MainHand.Entry == PoolFisherSettings.Instance.Mainhand)
                {
                    PoolFisher.mainhandList.Add(StyxWoW.Me.Inventory.Equipped.MainHand);
                    comboMainhand.Items.Add(StyxWoW.Me.Inventory.Equipped.MainHand.Name);
                    comboMainhand.SelectedItem = StyxWoW.Me.Inventory.Equipped.MainHand.Name;
                }
            }

            if (PoolFisherSettings.Instance.Offhand != 0)
            {
                foreach (WoWItem i in PoolFisher.BagItems)
                {
                    if (i.Entry == PoolFisherSettings.Instance.Offhand)
                    {
                        PoolFisher.offhandList.Add(i);
                        comboOffhand.Items.Add(i.Name);
                        comboOffhand.SelectedItem = i.Name;
                    }
                }

                if (StyxWoW.Me.Inventory.Equipped.OffHand != null && StyxWoW.Me.Inventory.Equipped.OffHand.Entry == PoolFisherSettings.Instance.Offhand)
                {
                    PoolFisher.offhandList.Add(StyxWoW.Me.Inventory.Equipped.OffHand);
                    comboOffhand.Items.Add(StyxWoW.Me.Inventory.Equipped.OffHand.Name);
                    comboOffhand.SelectedItem = StyxWoW.Me.Inventory.Equipped.OffHand.Name;
                }
            }
            
            #endregion
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            int MaxLocAttempts;
            int.TryParse(MaxNewLocAttemptsText.Text, out MaxLocAttempts);
            int MaxCastAttempts;
            int.TryParse(MaxCastAttemptsText.Text, out MaxCastAttempts);
            int MinCastRange;
            int.TryParse(MinCastRangeText.Text, out MinCastRange);
            int MaxCastRange;
            int.TryParse(MaxCastRangeText.Text, out MaxCastRange);

            PoolFisherSettings.Instance.NinjaPools = checkNinjaPools.Checked;
            PoolFisherSettings.Instance.MinCastRange = MinCastRange;
            PoolFisherSettings.Instance.MaxCastRange = MaxCastRange;
            PoolFisherSettings.Instance.MaxNewLocAttempts = MaxLocAttempts;
            PoolFisherSettings.Instance.MaxCastAttempts = MaxCastAttempts;
            PoolFisherSettings.Instance.HeightModifier = (int)numericHeightMod.Value;

            #region Bags full

            if (radioHearthAndExit.Checked)
            {
                PoolFisherSettings.Instance.ShouldMail = false;
                PoolFisherSettings.Instance.MailRecipient = "";
            }
            else
            {
                PoolFisherSettings.Instance.ShouldMail = true;
                PoolFisherSettings.Instance.MailRecipient = textMailTo.Text;
                LevelbotSettings.Instance.Load();
                LevelbotSettings.Instance.MailRecipient = PoolFisherSettings.Instance.MailRecipient;
                LevelbotSettings.Instance.Save();
            }

            #endregion

            #region Lures

            if (checkUseLure.Checked)
            {
                var item = comboLures.SelectedItem;
                if (item is Helpers.LureType)
                {
                    var lure = (Helpers.LureType)item;
                    PoolFisherSettings.Instance.useLure = true;
                    PoolFisherSettings.Instance.LureID = (int)lure.ID;
                }
            }
            else
            {
                PoolFisherSettings.Instance.useLure = false;
                PoolFisherSettings.Instance.LureID = 0;
            }

            #endregion

            #region Blacklist

            PoolFisherSettings.Instance.BlacklistSchools = checkBlacklistSchools.Checked;

            if (checkBlacklistSchools.Checked)
            {
                /// Cataclysm
                PoolFisherSettings.Instance.BLAlbinoCavefish    = checkBLAlbinoCavefish.Checked;
                PoolFisherSettings.Instance.BLAlgaefinRockfish  = checkBLAlgaefinRockfish.Checked;
                PoolFisherSettings.Instance.BLBlackbellyMudfish = checkBLBlackbellyMudfish.Checked;
                PoolFisherSettings.Instance.BLFathomEel         = checkBLFathomEel.Checked;
                PoolFisherSettings.Instance.BLHighlandGuppy     = checkBLHighlandGuppy.Checked;
                PoolFisherSettings.Instance.BLMountainTrout     = checkBLMountainTrout.Checked;
                PoolFisherSettings.Instance.BLPoolofFire        = checkBLPoolofFire.Checked;
                PoolFisherSettings.Instance.BLShipwreckDebris   = checkBLShipwreckDebris.Checked;
                /// Wrath of the Lich King
                PoolFisherSettings.Instance.BLBoreanManOWar         = checkBLBoreanManOWar.Checked;
                PoolFisherSettings.Instance.BLDeepSeaMonsterbelly   = checkBLDeepSeaMonsterbelly.Checked;
                PoolFisherSettings.Instance.BLDragonfinAngelfish    = checkBLDragonfinAngelfish.Checked;
                PoolFisherSettings.Instance.BLFangtoothHerring      = checkBLFangtoothHerring.Checked;
                PoolFisherSettings.Instance.BLGlacialSalmon         = checkBLGlacialSalmon.Checked;
                PoolFisherSettings.Instance.BLGlassfinMinnow        = checkBLGlassfinMinnow.Checked;
                PoolFisherSettings.Instance.BLImperialMantaRay      = checkBLImperialMantaRay.Checked;
                PoolFisherSettings.Instance.BLMoonglowCuttlefish    = checkBLMoonglowCuttlefish.Checked;
                PoolFisherSettings.Instance.BLMusselbackSculpin     = checkBLMusselbackSculpin.Checked;
                PoolFisherSettings.Instance.BLNettlefish            = checkBLNettlefish.Checked;
                /// Burning Crusade 
                PoolFisherSettings.Instance.BLBluefish          = checkBLBluefish.Checked;
                PoolFisherSettings.Instance.BLBrackishMix       = checkBLBrackishMix.Checked;
                PoolFisherSettings.Instance.BLHighlandMix       = checkBLHighlandMix.Checked;
                PoolFisherSettings.Instance.BLMudfish           = checkBLMudfish.Checked;
                PoolFisherSettings.Instance.BLPureWater         = checkBLPureWater.Checked;
                PoolFisherSettings.Instance.BLDarter            = checkBLDarter.Checked;
                PoolFisherSettings.Instance.BLSporefish         = checkBLSporefish.Checked;
                PoolFisherSettings.Instance.BLSteamPumpFlotsam  = checkBLSteamPumpFlotsam.Checked;
                Helpers.blacklistSchoolsFromSettings();
            }
            else
            {
                /// Cataclysm
                PoolFisherSettings.Instance.BLAlbinoCavefish    = false;
                PoolFisherSettings.Instance.BLAlgaefinRockfish  = false;
                PoolFisherSettings.Instance.BLBlackbellyMudfish = false;
                PoolFisherSettings.Instance.BLFathomEel         = false;
                PoolFisherSettings.Instance.BLHighlandGuppy     = false;
                PoolFisherSettings.Instance.BLMountainTrout     = false;
                PoolFisherSettings.Instance.BLPoolofFire        = false;
                PoolFisherSettings.Instance.BLShipwreckDebris   = false;
                /// Wrath of the Lich King
                PoolFisherSettings.Instance.BLBoreanManOWar         = false;
                PoolFisherSettings.Instance.BLDeepSeaMonsterbelly   = false;
                PoolFisherSettings.Instance.BLDragonfinAngelfish    = false;
                PoolFisherSettings.Instance.BLFangtoothHerring      = false;
                PoolFisherSettings.Instance.BLGlacialSalmon         = false;
                PoolFisherSettings.Instance.BLGlassfinMinnow        = false;
                PoolFisherSettings.Instance.BLImperialMantaRay      = false;
                PoolFisherSettings.Instance.BLMoonglowCuttlefish    = false;
                PoolFisherSettings.Instance.BLMusselbackSculpin     = false;
                PoolFisherSettings.Instance.BLNettlefish            = false;
                /// Burning Crusade
                PoolFisherSettings.Instance.BLBluefish          = false;
                PoolFisherSettings.Instance.BLBrackishMix       = false;
                PoolFisherSettings.Instance.BLHighlandMix       = false;
                PoolFisherSettings.Instance.BLMudfish           = false;
                PoolFisherSettings.Instance.BLPureWater         = false;
                PoolFisherSettings.Instance.BLDarter            = false;
                PoolFisherSettings.Instance.BLSporefish         = false;
                PoolFisherSettings.Instance.BLSteamPumpFlotsam  = false;
                Helpers.blacklistSchoolsFromSettings();
            }

            #endregion

            #region Mounts

            WoWSpell SwiftFlightForm = WoWSpell.FromId(40120);
            WoWSpell FlightForm = WoWSpell.FromId(33943);

            if (SpellManager.HasSpell(SwiftFlightForm) && (string)comboMounts.SelectedItem == SwiftFlightForm.Name)
            {
                PoolFisherSettings.Instance.FlyingMountID = SwiftFlightForm.Id;
            }
            if (SpellManager.HasSpell(FlightForm) && (string)comboMounts.SelectedItem == FlightForm.Name)
            {
                PoolFisherSettings.Instance.FlyingMountID = FlightForm.Id;
            }
            foreach (MountHelper.MountWrapper flyingMount in MountHelper.FlyingMounts)
            {
                if ((string)comboMounts.SelectedItem == flyingMount.Name)
                {
                    PoolFisherSettings.Instance.FlyingMountID = flyingMount.CreatureSpellId;
                }
            }

            #endregion

            #region Mode

            if (comboMode.SelectedIndex == 1)
            {
                PoolFisherSettings.Instance.BounceMode = true;
            }
            else
            {
                PoolFisherSettings.Instance.BounceMode = false;
            }

            #endregion

            #region Pole and Weapons

            if (PoolFisher.poleList.Count > 0 && comboPole.SelectedIndex != -1)
            {
                foreach (WoWItem i in PoolFisher.poleList)
                {
                    if (i.Name == comboPole.SelectedItem.ToString())
                    {
                        PoolFisherSettings.Instance.FishingPole = (int)i.Entry;
                    }
                }
            }
            else
            {
                PoolFisherSettings.Instance.FishingPole = 0;
            }

            if (PoolFisher.mainhandList.Count > 0 && comboMainhand.SelectedIndex != -1)
            {
                foreach (WoWItem i in PoolFisher.mainhandList)
                {
                    if (i.Name == comboMainhand.SelectedItem.ToString())
                    {
                        PoolFisherSettings.Instance.Mainhand = (int)i.Entry;
                    }
                }
            }
            else
            {
                PoolFisherSettings.Instance.Mainhand = 0;
            }

            if (PoolFisher.offhandList.Count > 0 && comboOffhand.SelectedIndex != -1)
            {
                foreach (WoWItem i in PoolFisher.offhandList)
                {
                    if (i.Name == comboOffhand.SelectedItem.ToString())
                    {
                        PoolFisherSettings.Instance.Offhand = (int)i.Entry;
                    }
                }
            }
            else
            {
                PoolFisherSettings.Instance.Offhand = 0;
            }

            #endregion

            #region Logging

            Logging.Write(System.Drawing.Color.Green, "Saved Settings:");
            Logging.Write(System.Drawing.Color.Green, "-------------------------------------------");
            Logging.Write(System.Drawing.Color.Green, "Flying Mount: {0}", (string)comboMounts.SelectedItem);
            Logging.Write(System.Drawing.Color.Green, "Fishing pole: {0}", (string)comboPole.SelectedItem);
            Logging.Write(System.Drawing.Color.Green, "Mainhand: {0}", (string)comboMainhand.SelectedItem);
            Logging.Write(System.Drawing.Color.Green, "Offhand: {0}", (string)comboOffhand.SelectedItem);

            Logging.Write(System.Drawing.Color.Green, "Height: {0}", PoolFisherSettings.Instance.HeightModifier);
            Logging.Write(System.Drawing.Color.Green, "Bouncemode: {0}", PoolFisherSettings.Instance.BounceMode);
            Logging.Write(System.Drawing.Color.Green, "Min. range to cast: {0}", PoolFisherSettings.Instance.MinCastRange);
            Logging.Write(System.Drawing.Color.Green, "Max. range to cast: {0}", PoolFisherSettings.Instance.MaxCastRange);
            Logging.Write(System.Drawing.Color.Green, "Max. attempts to cast: {0}", PoolFisherSettings.Instance.MaxCastAttempts);
            Logging.Write(System.Drawing.Color.Green, "Ninja Pools: {0}", PoolFisherSettings.Instance.NinjaPools);
            Logging.Write(System.Drawing.Color.Green, "Blacklist Schools: {0}", PoolFisherSettings.Instance.BlacklistSchools);
            Logging.Write(System.Drawing.Color.Green, "Use Lure: {0}", PoolFisherSettings.Instance.useLure);
            Logging.Write(System.Drawing.Color.Green, "Max. attempts to reach pool: {0}", PoolFisherSettings.Instance.MaxNewLocAttempts);

            Logging.Write(System.Drawing.Color.Green, "-------------------------------------------");
            Helpers.blacklistSchoolsFromSettings();

            #endregion

            PoolFisherSettings.Instance.Save();
            Close();
        }

        private void buttonMonitor_Click(object sender, EventArgs e)
        {
            //FormFishMonitoring form = new FormFishMonitoring();
            //PoolFisher.MonitoringThread = new Thread(new ThreadStart(Helpers.StartMonitoring));
            //PoolFisher.MonitoringThread.Start();
        }

        private void checkBlacklistSchools_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBlacklistSchools.Checked)
            {
                tabControlBlacklist.Visible = true;
            }
            else
            {
                tabControlBlacklist.Visible = false;
            }
        }

        private void buttonChancel_Click(object sender, EventArgs e)
        {
            Logging.Write(System.Drawing.Color.Red, "Settings not saved!");
            Close();
        }

        private void TestButton_Click(object sender, EventArgs e)
        {
            Logging.Write("Count of badPoolPoints: {0}", PoolFisher.badPoolPoints.Count);

            

            WoWPoint test = StyxWoW.Me.Location;

            test.Z = Helpers.GetWaterSurface(StyxWoW.Me.Location);
            Logging.Write(System.Drawing.Color.Red, "Location: {0}", StyxWoW.Me.Location);
            Logging.Write(System.Drawing.Color.Red, "Water Surface: {0}", test);

            ObjectManager.Update();
            List<WoWGameObject> poolList = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.SubType == WoWGameObjectType.FishingHole && !Blacklist.Contains(o.Guid) && !PoolFisher.PermaBlacklist.Contains(o.Entry) && o.Distance2D <= 150 && o.Location.X != 0).OrderBy(o => o.Distance).ToList();
            foreach (WoWGameObject p in poolList)
                Logging.Write("{0} - Found - {1} - at a distance of {2}. Guid: {3}. Entry: {4}.", Helpers.TimeNow, p.Name, p.Distance, p.Guid, p.Entry);

            try
            {
                float groundz;
                Navigator.FindHeight(StyxWoW.Me.Location.X, StyxWoW.Me.Location.Y, out groundz);
                Logging.Write("{0} - Navigator groundz: {1}.", Helpers.TimeNow, groundz);
            }
            catch (Exception) { }

            WoWPoint ground = WoWPoint.Empty;

            GameWorld.TraceLine(new WoWPoint(StyxWoW.Me.Location.X, StyxWoW.Me.Location.Y, 10000), new WoWPoint(StyxWoW.Me.Location.X, StyxWoW.Me.Location.Y, -10000), GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures | GameWorld.CGWorldFrameHitFlags.HitTestBoundingModels | GameWorld.CGWorldFrameHitFlags.HitTestWMO, out ground);
            if (ground != WoWPoint.Empty)
            {
                Logging.Write("{0} - TraceLine groundz: {1}.", Helpers.TimeNow, ground.Z);
            }
        }

        private void checkUseLure_CheckedChanged(object sender, EventArgs e)
        {
            if (checkUseLure.Checked)
            {
                comboLures.SelectedItem = null;
                comboLures.Enabled = true;
            }
            else
            {
                comboLures.SelectedItem = null;
                comboLures.Enabled = false;
            }
        }

        private void buttonRefreshWeaponsAndPole_Click(object sender, EventArgs e)
        {
            PoolFisher.BagItems = StyxWoW.Me.BagItems;
            PoolFisher.poleList.Clear();
            PoolFisher.mainhandList.Clear();
            PoolFisher.offhandList.Clear();

            comboPole.Items.Clear();
            comboMainhand.Items.Clear();
            comboOffhand.Items.Clear();

            foreach (WoWItem i in PoolFisher.BagItems)
            {
                if (i.ItemInfo.IsWeapon && i.ItemInfo.WeaponClass != WoWItemWeaponClass.FishingPole && (i.ItemInfo.InventoryType == InventoryType.WeaponMainHand ||
                    i.ItemInfo.InventoryType == InventoryType.TwoHandWeapon) && StyxWoW.Me.CanEquipItem(i))
                {
                    if (!PoolFisher.mainhandList.Contains(i)) PoolFisher.mainhandList.Add(i);
                }
                if (i.ItemInfo.IsWeapon && (i.ItemInfo.InventoryType == InventoryType.WeaponOffHand ||
                    i.ItemInfo.InventoryType == InventoryType.Weapon) && StyxWoW.Me.CanEquipItem(i))
                {
                    if (!PoolFisher.offhandList.Contains(i)) PoolFisher.offhandList.Add(i);
                }
                if (i.ItemInfo.IsWeapon && i.ItemInfo.WeaponClass == WoWItemWeaponClass.FishingPole
                     && StyxWoW.Me.CanEquipItem(i))
                {
                    if (!PoolFisher.poleList.Contains(i)) PoolFisher.poleList.Add(i);
                }
            }

            if (StyxWoW.Me.Inventory.Equipped.MainHand != null && StyxWoW.Me.Inventory.Equipped.MainHand.ItemInfo.WeaponClass != WoWItemWeaponClass.FishingPole)
            {
                if (!PoolFisher.mainhandList.Contains(StyxWoW.Me.Inventory.Equipped.MainHand)) PoolFisher.mainhandList.Add(StyxWoW.Me.Inventory.Equipped.MainHand);
            }

            if (StyxWoW.Me.Inventory.Equipped.MainHand != null && StyxWoW.Me.Inventory.Equipped.MainHand.ItemInfo.WeaponClass == WoWItemWeaponClass.FishingPole)
            {
                if (!PoolFisher.poleList.Contains(StyxWoW.Me.Inventory.Equipped.MainHand)) PoolFisher.poleList.Add(StyxWoW.Me.Inventory.Equipped.MainHand);
            }

            if (StyxWoW.Me.Inventory.Equipped.OffHand != null)
            {
                if (!PoolFisher.offhandList.Contains(StyxWoW.Me.Inventory.Equipped.OffHand)) PoolFisher.offhandList.Add(StyxWoW.Me.Inventory.Equipped.OffHand);
            }

            if (PoolFisher.poleList.Count > 0)
            {
                //comboPole.Items.Add("");

                foreach (WoWItem i in PoolFisher.poleList)
                {
                    if (i.Name != null)
                        comboPole.Items.Add(i.Name);
                }
            }
            else
            {
                comboPole.Text = "Nothing found..";
            }

            if (PoolFisher.mainhandList.Count > 0)
            {
                //comboMainhand.Items.Add("");

                foreach (WoWItem i in PoolFisher.mainhandList)
                {
                    if (i.Name != null)
                        comboMainhand.Items.Add(i.Name);
                }
            }
            else
            {
                comboMainhand.Text = "Nothing found..";
            }

            if (PoolFisher.offhandList.Count > 0)
            {
                //comboOffhand.Items.Add("");

                foreach (WoWItem i in PoolFisher.offhandList)
                {
                    if (i.Name != null)
                        comboOffhand.Items.Add(i.Name);
                }
            }
            else
            {
                comboOffhand.Text = "Nothing found..";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
            //Global.ShouldMail = true;
            //LevelBot.CreateVendorBehavior();
            //BotPoi.Current = new BotPoi(ProfileManager.CurrentProfile.MailboxManager.GetClosestMailbox().Location, PoiType.Mail);
            Styx.Global.ShouldMail = true;
            Styx.Logic.Vendors.ForceMail = true;
        }

        private void radioMailTo_CheckedChanged(object sender, EventArgs e)
        {
            if (radioMailTo.Checked)
            {
                textMailTo.Enabled = true;
            }
            else
            {
                textMailTo.Enabled = false;
            }
        }

        private void radioHearthAndExit_CheckedChanged(object sender, EventArgs e)
        {
            if (radioHearthAndExit.Checked)
            {
                textMailTo.Enabled = false;
            }
            else
            {
                textMailTo.Enabled = true;
            }
        }
    }
}
