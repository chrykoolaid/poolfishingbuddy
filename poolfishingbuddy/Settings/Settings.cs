using System.IO;
using System.Windows.Forms;
using Styx;
using Styx.Helpers;

namespace PoolFishingBuddy
{
    public class PoolFisherSettings : Settings
    {
        public static PoolFisherSettings Instance = new PoolFisherSettings();

        public PoolFisherSettings() : base(Path.Combine(Application.StartupPath, string.Format(@"Settings\PoolFishingBuddy\{0}.xml", StyxWoW.Me != null ? StyxWoW.Me.Name : "")))
        {
            //Load();
        }

        [Setting(Explanation = "Entry for custom flying mount."), DefaultValue(0)]
        public int FlyingMountID { get; set; }

        [Setting(Explanation = "Bounce mode on or not."), DefaultValue(false)]
        public bool BounceMode { get; set; }

        [Setting(Explanation = "The maximum range of locations to fish from."), DefaultValue(20)]
        public int MaxCastRange { get; set; }

        [Setting(Explanation = "The maximum value of tries to cast fishing."), DefaultValue(10)]
        public int MaxCastAttempts { get; set; }

        [Setting(Explanation = "Ninja pools or not."), DefaultValue(false)]
        public bool NinjaPools { get; set; }

        [Setting(Explanation = "Use lure or not."), DefaultValue(false)]
        public bool useLure { get; set; }

        [Setting(Explanation = "ID for lure item."), DefaultValue(0)]
        public int LureID { get; set; }

        [Setting(Explanation = "ID for mainhand item."), DefaultValue(0)]
        public int MainHand { get; set; }

        [Setting(Explanation = "ID for offhand item."), DefaultValue(0)]
        public int OffHand { get; set; }

        [Setting(Explanation = "Descend on higher reaches or not."), DefaultValue(false)]
        public bool DescendHigher { get; set; }

        [Setting(Explanation = "The maximum value of tries to descend to ground."), DefaultValue(5)]
        public int MaxNewLocAttempts { get; set; }

        #region Blacklist

        [Setting(Explanation = "Blacklist schools or not."), DefaultValue(false)]
        public bool BlacklistSchools { get; set; }

        [Setting(Explanation = "Blacklist Combo last Value."), DefaultValue(null)]
        public string BlacklistComboValue { get; set; }

        [Setting(Explanation = "Blacklist Albino Cavefish."), DefaultValue(false)]
        public bool BLAlbinoCavefish { get; set; }

        [Setting(Explanation = "Blacklist Algaefin Rockfish."), DefaultValue(false)]
        public bool BLAlgaefinRockfish { get; set; }

        [Setting(Explanation = "Blacklist Blackbelly Mudfish."), DefaultValue(false)]
        public bool BLBlackbellyMudfish { get; set; }

        [Setting(Explanation = "Blacklist Fathom Eel."), DefaultValue(false)]
        public bool BLFathomEel { get; set; }

        [Setting(Explanation = "Blacklist Highland Guppy."), DefaultValue(false)]
        public bool BLHighlandGuppy { get; set; }

        [Setting(Explanation = "Blacklist Mountain Trout."), DefaultValue(false)]
        public bool BLMountainTrout { get; set; }

        [Setting(Explanation = "Blacklist Pool of Fire."), DefaultValue(false)]
        public bool BLPoolofFire { get; set; }

        [Setting(Explanation = "Blacklist Shipwreck Debris."), DefaultValue(false)]
        public bool BLShipwreckDebris { get; set; }

        #endregion
    }
}
