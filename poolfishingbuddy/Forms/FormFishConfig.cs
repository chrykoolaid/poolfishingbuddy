using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

using Styx;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

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

            try
            {
                pictureBox1.Image = Image.FromStream(new MemoryStream(new WebClient().DownloadData("http://poolfishingbuddy.googlecode.com/svn/images/fish.png")));
                Icon = new Icon(new MemoryStream(new WebClient().DownloadData("http://poolfishingbuddy.googlecode.com/svn/images/fish.ico")), 32, 32);
            }
            catch (InvalidCastException ex)
            {
                Logging.Write(System.Drawing.Color.Red, "{0} - Exception: {1}", Helpers.TimeNow, ex);
            }

            if (PoolFisherSettings.Instance.useCustomCastRange)
            {
                checkCustomCastRange.Checked = PoolFisherSettings.Instance.useCustomCastRange;
                CastRangeText.Enabled = PoolFisherSettings.Instance.useCustomCastRange;
                CastRangeText.Text = PoolFisherSettings.Instance.CastRange.ToString();
            }
            else
            {
                checkCustomCastRange.Checked = PoolFisherSettings.Instance.useCustomCastRange;
                CastRangeText.Enabled = PoolFisherSettings.Instance.useCustomCastRange;
                CastRangeText.Text = "15";
            }

            MaxTriesCastingText.Text = PoolFisherSettings.Instance.MaxTriesCasting;
            MaxTriesDescendText.Text = PoolFisherSettings.Instance.MaxTriesDescend;

            checkNinjaPools.Checked = PoolFisherSettings.Instance.NinjaPools;
            checkBlacklistSchools.Checked = PoolFisherSettings.Instance.BlacklistSchools;

            checkDescendHigher.Checked = PoolFisherSettings.Instance.DescendHigher;

            #region Blacklist Schools

            checkBlacklistSchools.Checked = PoolFisherSettings.Instance.BlacklistSchools;

            if (PoolFisherSettings.Instance.BlacklistSchools)
            {
                comboBlacklist.Enabled = true;
                comboBlacklist.SelectedItem = (string) PoolFisherSettings.Instance.BlacklistComboValue;

                if (comboBlacklist.SelectedText.Contains("Cataclysm"))
                {
                    groupBoxCata.Visible = true;
                }

                checkBLAlbinoCavefish.Checked       = PoolFisherSettings.Instance.BLAlbinoCavefish;
                checkBLAlgaefinRockfish.Checked     = PoolFisherSettings.Instance.BLAlgaefinRockfish;
                checkBLBlackbellyMudfish.Checked    = PoolFisherSettings.Instance.BLBlackbellyMudfish;
                checkBLFathomEel.Checked            = PoolFisherSettings.Instance.BLFathomEel;
                checkBLHighlandGuppy.Checked        = PoolFisherSettings.Instance.BLHighlandGuppy;
                checkBLMountainTrout.Checked        = PoolFisherSettings.Instance.BLMountainTrout;
                checkBLPoolofFire.Checked           = PoolFisherSettings.Instance.BLPoolofFire;
                checkBLShipwreckDebris.Checked      = PoolFisherSettings.Instance.BLShipwreckDebris;
            }
            else
            {
                comboBlacklist.Enabled              = false;
                groupBoxCata.Visible                = false;
                checkBLAlbinoCavefish.Checked       = false;
                checkBLAlgaefinRockfish.Checked     = false;
                checkBLBlackbellyMudfish.Checked    = false;
                checkBLFathomEel.Checked            = false;
                checkBLHighlandGuppy.Checked        = false;
                checkBLMountainTrout.Checked        = false;
                checkBLPoolofFire.Checked           = false;
                checkBLShipwreckDebris.Checked      = false;
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

            Logging.Write(SwiftFlightForm.Name);
            Logging.Write(FlightForm.Name);

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
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (checkCustomCastRange.Checked)
            {
                int CastRange;
                int.TryParse(CastRangeText.Text, out CastRange);
                if (CastRange < 10)
                    CastRange = 10;
                else if (CastRange > 20)
                    CastRange = 20;

                PoolFisherSettings.Instance.CastRange = CastRange;
                PoolFisherSettings.Instance.useCustomCastRange = checkCustomCastRange.Checked;
            }
            else
            {
                PoolFisherSettings.Instance.CastRange = 15;
                PoolFisherSettings.Instance.useCustomCastRange = checkCustomCastRange.Checked;
            }

            PoolFisherSettings.Instance.NinjaPools = checkNinjaPools.Checked;
            PoolFisherSettings.Instance.DescendHigher = checkDescendHigher.Checked;

            PoolFisherSettings.Instance.MaxTriesDescend = MaxTriesDescendText.Text;
            PoolFisherSettings.Instance.MaxTriesCasting = MaxTriesCastingText.Text;

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
                PoolFisherSettings.Instance.BlacklistComboValue = (string)comboBlacklist.SelectedItem;
                PoolFisherSettings.Instance.BLAlbinoCavefish = checkBLAlbinoCavefish.Checked;
                PoolFisherSettings.Instance.BLAlgaefinRockfish = checkBLAlgaefinRockfish.Checked;
                PoolFisherSettings.Instance.BLBlackbellyMudfish = checkBLBlackbellyMudfish.Checked;
                PoolFisherSettings.Instance.BLFathomEel = checkBLFathomEel.Checked;
                PoolFisherSettings.Instance.BLHighlandGuppy = checkBLHighlandGuppy.Checked;
                PoolFisherSettings.Instance.BLMountainTrout = checkBLMountainTrout.Checked;
                PoolFisherSettings.Instance.BLPoolofFire = checkBLPoolofFire.Checked;
                PoolFisherSettings.Instance.BLShipwreckDebris = checkBLShipwreckDebris.Checked;
            }
            else
            {
                PoolFisherSettings.Instance.BlacklistComboValue = null;
                PoolFisherSettings.Instance.BLAlbinoCavefish = false;
                PoolFisherSettings.Instance.BLAlgaefinRockfish = false;
                PoolFisherSettings.Instance.BLBlackbellyMudfish = false;
                PoolFisherSettings.Instance.BLFathomEel = false;
                PoolFisherSettings.Instance.BLHighlandGuppy = false;
                PoolFisherSettings.Instance.BLMountainTrout = false;
                PoolFisherSettings.Instance.BLPoolofFire = false;
                PoolFisherSettings.Instance.BLShipwreckDebris = false;
            }

            #endregion

            #region Mounts

            WoWSpell SwiftFlightForm = WoWSpell.FromId(40120);
            WoWSpell FlightForm = WoWSpell.FromId(33943);

            Logging.Write(SwiftFlightForm.Name);
            Logging.Write(FlightForm.Name);

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

            PoolFisherSettings.Instance.Save();

            Logging.Write(System.Drawing.Color.Green, "Saved Settings:");
            Logging.Write(System.Drawing.Color.Green, "-------------------------------------------");
            Logging.Write(System.Drawing.Color.Green, "Flying Mount: {0}", (string)comboMounts.SelectedItem);
            Logging.Write(System.Drawing.Color.Green, "Cast Range: {0}", PoolFisherSettings.Instance.CastRange);
            Logging.Write(System.Drawing.Color.Green, "Max. tries to cast: {0}", PoolFisherSettings.Instance.MaxTriesCasting);
            Logging.Write(System.Drawing.Color.Green, "Ninja Pools: {0}", PoolFisherSettings.Instance.NinjaPools);
            Logging.Write(System.Drawing.Color.Green, "Blacklist Schools: {0}", PoolFisherSettings.Instance.BlacklistSchools);
            Logging.Write(System.Drawing.Color.Green, "Use Lure: {0}", PoolFisherSettings.Instance.useLure);
            Logging.Write(System.Drawing.Color.Green, "Descend higher: {0}", PoolFisherSettings.Instance.DescendHigher);
            Logging.Write(System.Drawing.Color.Green, "Max. tries to descend: {0}", PoolFisherSettings.Instance.MaxTriesDescend);

            Logging.Write(System.Drawing.Color.Green, "-------------------------------------------");
            Helpers.blacklistSchoolsFromSettings();

            Close();
        }

        private void buttonChancel_Click(object sender, EventArgs e)
        {
            Logging.Write(System.Drawing.Color.Red, "Settings not saved!");
            Close();
        }

        private void checkCustomCastRange_CheckedChanged(object sender, EventArgs e)
        {
            CastRangeText.Enabled = checkCustomCastRange.Checked;
        }

        private void checkBlacklistSchools_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBlacklistSchools.Checked)
            {
                comboBlacklist.Enabled = true;
                comboBlacklist.SelectedItem = null;
            }
            else
            {
                comboBlacklist.Enabled = false;
                groupBoxCata.Visible = false;
                comboBlacklist.SelectedItem = null;
            }
        }

        private void ComboBlacklist_SelectedIndexChanged(object sender, EventArgs e)
        {
            if ((string)comboBlacklist.SelectedItem == "Cataclysm")
                groupBoxCata.Visible = true;
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

        private void TestButton_Click(object sender, EventArgs e)
        {
            WoWPoint test = StyxWoW.Me.Location;

            test.Z = Helpers.GetWaterSurface(StyxWoW.Me.Location);
            Logging.Write(System.Drawing.Color.Red, "Location: {0}", StyxWoW.Me.Location);
            Logging.Write(System.Drawing.Color.Red, "Water Surface: {0}", test);
        }

        private void buttonMonitor_Click(object sender, EventArgs e)
        {
            //FormFishMonitoring form = new FormFishMonitoring();
            //PoolFisher.MonitoringThread = new Thread(new ThreadStart(Helpers.StartMonitoring));
            //PoolFisher.MonitoringThread.Start();
        }
    }
}
