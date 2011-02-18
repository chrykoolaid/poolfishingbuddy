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

        [Setting(Explanation = "Modifier for height of profiles z-coords."), DefaultValue(0)]
        public int HeightModifier { get; set; }

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

        [Setting(Explanation = "ID for fishing pole."), DefaultValue(0)]
        public int FishingPole { get; set; }

        [Setting(Explanation = "ID for mainhand item."), DefaultValue(0)]
        public int Mainhand { get; set; }

        [Setting(Explanation = "ID for offhand item."), DefaultValue(0)]
        public int Offhand { get; set; }

        [Setting(Explanation = "The maximum value of tries to descend to ground."), DefaultValue(5)]
        public int MaxNewLocAttempts { get; set; }

        #region Blacklist

        #region Cataclysm

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

        #region Wrath of the Lichking

        /// ----------------
        /// Wrath of the Lichking
        /// ----------------
        /// Borean Man O' War / Boreanische Galeeren     - Entry: 192051
        /// Deep Sea Monsterbelly / Tiefseemonsterbauch  - Entry: 192053
        /// Dragonfin Angelfish / Engelsdrachenfisch     - Entry: 192048
        /// Fangtooth Herring / Fangzahnhering           - Entry: 192049
        /// Glacial Salmon / Winterlachs                 - Entry: 192050
        /// Glassfin Minnow / Glasflossenelritze         - Entry: 192059
        /// Imperial Manta Ray / Imperialer Mantarochen  - Entry: 192052
        /// Moonglow Cuttlefish / Mondlichtsepia         - Entry: 192054
        /// Musselback Sculpin / Muschelrückengroppe     - Entry: 192046
        /// Nettlefish / Nesselfisch                     - Entry: 192057

        [Setting(Explanation = "Blacklist Borean Man O' War."), DefaultValue(false)]
        public bool BLBoreanManOWar { get; set; }

        [Setting(Explanation = "Blacklist Deep Sea Monsterbelly."), DefaultValue(false)]
        public bool BLDeepSeaMonsterbelly { get; set; }

        [Setting(Explanation = "Blacklist Dragonfin Angelfish."), DefaultValue(false)]
        public bool BLDragonfinAngelfish { get; set; }

        [Setting(Explanation = "Blacklist Fangtooth Herring."), DefaultValue(false)]
        public bool BLFangtoothHerring { get; set; }

        [Setting(Explanation = "Blacklist Glacial Salmon."), DefaultValue(false)]
        public bool BLGlacialSalmon { get; set; }

        [Setting(Explanation = "Blacklist Glassfin Minnow."), DefaultValue(false)]
        public bool BLGlassfinMinnow { get; set; }

        [Setting(Explanation = "Blacklist Imperial Manta Ray."), DefaultValue(false)]
        public bool BLImperialMantaRay { get; set; }

        [Setting(Explanation = "Blacklist Moonglow Cuttlefish."), DefaultValue(false)]
        public bool BLMoonglowCuttlefish { get; set; }

        [Setting(Explanation = "Blacklist Musselback Sculpin."), DefaultValue(false)]
        public bool BLMusselbackSculpin { get; set; }

        [Setting(Explanation = "Blacklist Nettlefish."), DefaultValue(false)]
        public bool BLNettlefish { get; set; }

        #endregion

        #region Burning Crusade

        /// ----------------
        /// Burning Crusade
        /// ----------------
        /// Bluefish / Blauflosse                           - Entry: 182959
        /// Brackish Mix / Brackwasserschwarm               - Entry: 182954
        /// Highland Mix / Hochlandschwarm                  - Entry: 182957
        /// Mudfish / Matschflosser                         - Entry: 182958
        /// Pure Water / Reines Wasser                      - Entry: 182951
        /// Darter / Stachelflosser                         - Entry: 182956
        /// Sporefish / Sporenfisch                         - Entry: 182953
        /// Steam Pump Flotsam / Treibgut der Dampfpumpe    - Entry: 182952

        [Setting(Explanation = "Blacklist Bluefish."), DefaultValue(false)]
        public bool BLBluefish { get; set; }

        [Setting(Explanation = "Blacklist Brackish Mixed."), DefaultValue(false)]
        public bool BLBrackishMix { get; set; }

        [Setting(Explanation = "Blacklist Highland Mixed."), DefaultValue(false)]
        public bool BLHighlandMix { get; set; }

        [Setting(Explanation = "Blacklist Mudfish."), DefaultValue(false)]
        public bool BLMudfish { get; set; }

        [Setting(Explanation = "Blacklist Pure Water."), DefaultValue(false)]
        public bool BLPureWater { get; set; }

        [Setting(Explanation = "Blacklist Darter."), DefaultValue(false)]
        public bool BLDarter { get; set; }

        [Setting(Explanation = "Blacklist Sporefish."), DefaultValue(false)]
        public bool BLSporefish { get; set; }

        [Setting(Explanation = "Blacklist Steam Pump Flotsam."), DefaultValue(false)]
        public bool BLSteamPumpFlotsam { get; set; }

        #endregion

        #endregion
    }
}
