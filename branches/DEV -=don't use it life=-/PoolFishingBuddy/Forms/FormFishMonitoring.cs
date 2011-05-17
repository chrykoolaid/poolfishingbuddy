using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;
using System.Threading;
using System.Security.Permissions;


using Styx;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace PoolFishingBuddy.Forms
{
    public partial class FormFishMonitoring : Form
    {
        public FormFishMonitoring()
        {
            InitializeComponent();
        }

        private void FormFishMonitoring_Load(object sender, EventArgs e)
        {
            try
            {
                Icon = new Icon(new MemoryStream(new WebClient().DownloadData("http://poolfishingbuddy.googlecode.com/svn/images/fish.ico")), 32, 32);
            }
            catch (InvalidCastException ex)
            {
                Logging.Write(System.Drawing.Color.Red, "{0} - Exception: {1}", Helpers.TimeNow, ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception message: {0}", ex.Message);
                Thread.ResetAbort();
            }

            if (StyxWoW.Me.IsValid && StyxWoW.IsInGame)
            {
                PoolFisher.GetValuesThread = new Thread(new ThreadStart(GetValues));
                PoolFisher.GetValuesThread.Start();
            }
        }

        private void UpdateValues()
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(2000);
            GetValues();
        }

        private void GetValues()
        {
            try
            {
                if (StyxWoW.Me.IsValid && StyxWoW.IsInGame)
                {
                    Bar1.Maximum = (int)StyxWoW.Me.MaxHealth;
                    Bar1.Value = (int)StyxWoW.Me.CurrentHealth;



                    label2Bar1.Text = StyxWoW.Me.CurrentHealth.ToString();
                    label4Bar1.Text = StyxWoW.Me.MaxHealth.ToString();

                    if (StyxWoW.Me.CurrentPowerInfo.Type.ToString() == "Mana")
                    {
                        Bar2.Maximum = (int)StyxWoW.Me.MaxMana;
                        Bar2.Value = (int)StyxWoW.Me.CurrentMana;

                        label2ndStat.Text = "Mana";
                        label2Bar2.Text = StyxWoW.Me.CurrentMana.ToString();
                        label4Bar2.Text = StyxWoW.Me.MaxMana.ToString();
                    }
                    else if (StyxWoW.Me.CurrentPowerInfo.Type.ToString() == "Focus")
                    {
                        Bar2.Maximum = (int)StyxWoW.Me.MaxFocus;
                        Bar2.Value = (int)StyxWoW.Me.CurrentFocus;

                        label2ndStat.Text = "Focus";
                        label2Bar2.Text = StyxWoW.Me.CurrentFocus.ToString();
                        label4Bar2.Text = StyxWoW.Me.MaxFocus.ToString();
                    }
                    else if (StyxWoW.Me.CurrentPowerInfo.Type.ToString() == "Rage")
                    {
                        Bar2.Maximum = (int)StyxWoW.Me.MaxRage;
                        Bar2.Value = (int)StyxWoW.Me.CurrentRage;

                        label2ndStat.Text = "Rage";
                        label2Bar2.Text = StyxWoW.Me.CurrentRage.ToString();
                        label4Bar2.Text = StyxWoW.Me.MaxRage.ToString();
                    }
                    else if (StyxWoW.Me.CurrentPowerInfo.Type.ToString() == "Runic Power")
                    {
                        Bar2.Maximum = (int)StyxWoW.Me.MaxRunicPower;
                        Bar2.Value = (int)StyxWoW.Me.CurrentRunicPower;

                        label2ndStat.Text = "Runic Power";
                        label2Bar2.Text = StyxWoW.Me.CurrentRunicPower.ToString();
                        label4Bar2.Text = StyxWoW.Me.MaxRunicPower.ToString();
                    }
                    else if (StyxWoW.Me.CurrentPowerInfo.Type.ToString() == "Energy")
                    {
                        Bar2.Maximum = (int)StyxWoW.Me.MaxEnergy;
                        Bar2.Value = (int)StyxWoW.Me.CurrentEnergy;

                        label2ndStat.Text = "Energy";
                        label2Bar2.Text = StyxWoW.Me.CurrentEnergy.ToString();
                        label4Bar2.Text = StyxWoW.Me.MaxEnergy.ToString();
                    }

                    labelArea.Text = StyxWoW.Me.ZoneText;
                    labelLocation.Text = StyxWoW.Me.Location.ToString();
                }
                UpdateValues();
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("Exception message: {0}", ex.Message);
                Thread.ResetAbort();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception message: {0}", ex.Message);
                Thread.ResetAbort();
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                PoolFisher.GetValuesThread.Abort();
                PoolFisher.GetValuesThread.Join();
            }
            catch (ThreadAbortException tae)
            {
                Logging.WriteDebug("ThreadAbortException: {0}", tae);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception message: {0}", ex.Message);
                Thread.ResetAbort();
            }
            try
            {
                PoolFisher.MonitoringThread.Abort();
                PoolFisher.MonitoringThread.Join();
            }
            catch (ThreadAbortException tae)
            {
                Logging.WriteDebug("ThreadAbortException: {0}", tae);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception message: {0}", ex.Message);
                Thread.ResetAbort();
            }
            this.Close();
        }
    }
}
