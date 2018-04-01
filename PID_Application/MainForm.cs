﻿using System;
using System.Drawing;
using System.Windows.Forms;
using NationalInstruments.DAQmx;
using System.Timers;
using System.IO;

namespace PID_Application
{
    public partial class MainForm : Form
    {      
        //Form members
        private System.Windows.Forms.Timer formUpdateTimer;
        private System.Timers.Timer pidTimeStep;
        
        private TextBox[] textBoxes;
        private Label[] labels;
        private PID pidTask;

        private string[] names;
        private bool taskRunning;
        private string tempUnits;

        #region Form Handles
        public MainForm()
        {
            InitializeComponent();
            
            formUpdateTimer = new System.Windows.Forms.Timer();        
            formUpdateTimer.Tick += new EventHandler(MainForm_TimerTick);
            pidTimeStep = new System.Timers.Timer();
            pidTimeStep.Elapsed += new ElapsedEventHandler(PID_TimeStep);

            textBoxes = new TextBox[] { TempSetBx, ProportionalBx, IntegralBx, DerivativeBx, TimeStepBx };
            labels = new Label[] { TempLbl, ProportionalContributionLbl, IntegralContributionLbl, DerivativeContributionLbl, TimeStepsLbl, TimeToEqLbl };
            names = new string[textBoxes.Length];

            MenuProgramHelp.ShortcutKeys = Keys.Control | Keys.H;
            MenuQuit.ShortcutKeys = Keys.Control | Keys.Q;
            MenuList.ShortcutKeys = Keys.Control | Keys.M;
            MenuSelectUnitDisplay.ShortcutKeys = Keys.Control | Keys.U;
            MenuLockAParameter.ShortcutKeys = Keys.Control | Keys.A;
            MenuLockBParameter.ShortcutKeys = Keys.Control | Keys.B;
            MenuLockCParameter.ShortcutKeys = Keys.Control | Keys.C;
            MenuLockTimeStepSize.ShortcutKeys = Keys.Control | Keys.T;
            MenuDeviceRefresh.ShortcutKeys = Keys.Control | Keys.R;
            MenuQuit.ShowShortcutKeys = true;
            MenuProgramHelp.ShowShortcutKeys = true;
            MenuList.ShowShortcutKeys = true;
            MenuSelectUnitDisplay.ShowShortcutKeys = true;
            MenuLockAParameter.ShowShortcutKeys = true;
            MenuLockBParameter.ShowShortcutKeys = true;
            MenuLockCParameter.ShowShortcutKeys = true;
            MenuLockTimeStepSize.ShowShortcutKeys = true;
            MenuDeviceRefresh.ShowShortcutKeys = true;
      
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;

            formUpdateTimer.Interval = 100;
            pidTimeStep.Interval = 10;

            TempSetBx.Text = "22.0";
            ProportionalBx.Text = "0.0";
            IntegralBx.Text = "0.0";
            DerivativeBx.Text = "0.0";
            TimeStepBx.Text = "0.01";
            TimeStepBx.Enabled = false;

            ProportionalContributionLbl.Text = "0.0";
            IntegralContributionLbl.Text = "0.0";
            DerivativeContributionLbl.Text = "0.0";

            for (int i = 0; i < textBoxes.Length; i++)
            {
                textBoxes[i].KeyPress += new KeyPressEventHandler(Bx_KeyPress);
                textBoxes[i].TextChanged += new EventHandler(Bx_TextChanged);
                names[i] = textBoxes[i].Name;
            }

            try
            {
                DeviceBx.Items.AddRange(DaqSystem.Local.Devices);
                VoltChnnlBx.Items.AddRange(DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.AO, PhysicalChannelAccess.External));
                TempChnnlBx.Items.AddRange(DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.AI, PhysicalChannelAccess.External));

                if (DeviceBx.Items.Count != 0)
                    pidTask = new PID(DeviceBx.Items[0].ToString(), 0, 0, AITerminalConfiguration.Rse);
                else
                    pidTask = new PID(DeviceBx.Text, 0, 0, AITerminalConfiguration.Rse);

            }
            catch (DaqException)
            { 
                TempSetBx.Enabled = false;
                TimeStepBx.Enabled = false;
                ProportionalBx.Enabled = false;
                IntegralBx.Enabled = false;
                DerivativeBx.Enabled = false;
                StartStopBttn.Enabled = false;
                MessageBox.Show("No DAQ device is present. Please check device connection in NImax and refresh the device list.","Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
            finally
            {
                if (DeviceBx.Items.Count != 0)
                {
                    DeviceBx.SelectedIndex = 0;
                    VoltChnnlBx.SelectedIndex = 0;
                    TempChnnlBx.SelectedIndex = 0;
                }
                else
                {
                    DeviceBx.SelectedIndex = -1;
                    VoltChnnlBx.SelectedIndex = -1;
                    TempChnnlBx.SelectedIndex = -1;
                }
            }

            MeasureTypeBx.Items.Add("RSE");
            MeasureTypeBx.SelectedIndex = 0;

            taskRunning = false;

            TempPlot.ChartAreas[0].AxisY.LabelStyle.Format = "###.#";
            TempPlot.Series[0].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            TempPlot.Series[0].Name = "Temperature";
            TempPlot.Series.Add("Set Point").Color = Color.Green;
            TempPlot.Series[1].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            TempPlot.ChartAreas[0].AxisX.Title = "Time (ms)";
            TempPlot.ChartAreas[0].AxisY.Title = "Temperature (°C)";

            TempPlotExpanded.ChartAreas[0].AxisY.LabelStyle.Format = "###.#";
            TempPlotExpanded.Series[0].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            TempPlotExpanded.Series[0].Name = "Temperature";
            TempPlotExpanded.Series.Add("Set Point").Color = Color.Green;
            TempPlotExpanded.Series[1].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            TempPlotExpanded.ChartAreas[0].AxisX.Title = "Time (ms)";
            TempPlotExpanded.ChartAreas[0].AxisY.Title = "Temperature (°C)";

            Cursor.Current = Cursors.Default;
        }
      
        private void MenuQuit_Click(object sender, EventArgs e)
        {
            formUpdateTimer.Enabled = false;
            pidTimeStep.Enabled = false;
            Close();
        }    
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            formUpdateTimer.Stop();
            pidTimeStep.Stop();
            formUpdateTimer.Enabled = false;
            pidTimeStep.Enabled = false;
            formUpdateTimer.Dispose();
            pidTimeStep.Dispose();

            pidTask.Off();
            //pidTask.Dispose();
        }   
        #endregion

        #region TextBox Event Handles
        private void Bx_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.' && !char.IsControl(e.KeyChar) && e.KeyChar != '-')
            {
                e.Handled = true;
            }
            else if ((e.KeyChar == '.') && ((sender as TextBox).Text.IndexOf('.') > -1))
            {
                e.Handled = true;
            }
            else if ((e.KeyChar == '-'))
            {
                e.Handled = true;
            }
        }
        private void Bx_TextChanged(object sender,EventArgs e)
        {
            TextBox changed = (TextBox)sender;
            double numToWrite;
            int i = 0;

            foreach (string name in names){
                if(changed.Name == name){
                    i = Array.IndexOf(names,name);
                }
            }

            switch (i)
            {
                case 0:
                        if(double.TryParse(textBoxes[0].Text,out numToWrite))
                            pidTask.TempSet = numToWrite;
                        else
                            pidTask.TempSet = 0.0;
                        break;
                case 1:
                        if (double.TryParse(textBoxes[1].Text, out numToWrite))
                            pidTask.ProportionalTerm = numToWrite;
                        else
                            pidTask.ProportionalTerm = 0.0;
                        break;
                case 2:
                        if (double.TryParse(textBoxes[2].Text, out numToWrite))
                            pidTask.Integralterm = numToWrite;
                        else
                            pidTask.Integralterm = 0.0;
                        break;
                case 3:
                        if (double.TryParse(textBoxes[3].Text, out numToWrite))
                            pidTask.DerivativeTerm = numToWrite;
                        else
                            pidTask.DerivativeTerm = 0.0;
                        break;
                case 4:
                        if (double.TryParse(textBoxes[4].Text, out numToWrite))
                        {
                            pidTask.TimeStep = numToWrite;
                        }
                        else
                        {
                            pidTask.TimeStep = 0.01;
                        }                        
                        break;
            }

        }
        #endregion

        private void StartStopBttn_Click(object sender, EventArgs e)
        {
            if (!taskRunning)
            {
                double tempSet,timeStep,proportionalTerm,integralTerm,derivativeTerm; //C# 7 allows for inline declerations of out parameters

                StartStopBttn.Text = "Turn Control\nOff";

                try
                {
                    if (double.TryParse(TempSetBx.Text, out tempSet))
                    {
                        TempPlot.Series[1].Points.AddY(tempSet);
                    }
                    else
                    {
                        tempSet = 22.0;
                        TempSetBx.Text = "22.0";
                        TempPlot.Series[1].Points.AddY(tempSet);
                    }
                    if(!double.TryParse(TimeStepBx.Text, out timeStep))
                    {
                        timeStep = 0.01;
                        TimeStepBx.Text = "0.01";
                    }
                    if(!double.TryParse(ProportionalBx.Text,out proportionalTerm))
                    {
                        proportionalTerm = 0.0;
                        ProportionalBx.Text = "0.0";
                    }
                    if(!double.TryParse(IntegralBx.Text,out integralTerm))
                    {
                        integralTerm = 0.0;
                        IntegralBx.Text = "0.0";
                    }
                    if(!double.TryParse(DerivativeBx.Text,out derivativeTerm))
                    {
                        derivativeTerm = 0.0;
                        DerivativeBx.Text = "0.0";
                    }

                    pidTask = new PID(DeviceBx.Text, TempChnnlBx.SelectedIndex, VoltChnnlBx.SelectedIndex, AITerminalConfiguration.Rse) {
                        TimeStep = timeStep,
                        TempSet = tempSet,
                        ProportionalTerm = proportionalTerm,
                        Integralterm = integralTerm,
                        DerivativeTerm = derivativeTerm,
                        Units = tempUnits
                    };
                    
                    pidTask.Update();

                    formUpdateTimer.Enabled = true;
                    pidTimeStep.Enabled = true;
                    pidTimeStep.Interval = (int)(timeStep*1000);
                    pidTimeStep.Start();
                    formUpdateTimer.Start();

                    taskRunning = true;
                    VisualControlStatus.BackColor = Color.Green;
                    VisualControlStatusLbl.Text = "On";

                    MenuUnitCelsius.Enabled = false;
                    MenuUnitFarenheit.Enabled = false;
                }
                catch
                {
                    StartStopBttn.Text = "Turn Control\nOn";
                    VisualControlStatus.BackColor = Color.Red;
                    VisualControlStatusLbl.Text = "Off";
                    TempPlot.Series[0].Points.Clear();
                    TempPlot.Series[1].Points.Clear();
                    TempPlotExpanded.Series[0].Points.Clear();
                    TempPlotExpanded.Series[1].Points.Clear();

                    TempSetBx.Enabled = false;
                    TimeStepBx.Enabled = false;
                    ProportionalBx.Enabled = false;
                    IntegralBx.Enabled = false;
                    DerivativeBx.Enabled = false;
                    StartStopBttn.Enabled = false;
                    taskRunning = false;

                    MessageBox.Show("There was an error communicating with the device. Check devices status and refresh the device list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }
            else
            {
                pidTimeStep.Stop();
                pidTimeStep.Enabled = false;
                formUpdateTimer.Stop();
                formUpdateTimer.Enabled = false;

                pidTask.Off();

                TempPlot.Series[0].Points.Clear();
                TempPlot.Series[1].Points.Clear();
                TempPlotExpanded.Series[0].Points.Clear();
                TempPlotExpanded.Series[1].Points.Clear();

                StartStopBttn.Text = "Turn Control\nOn";
                taskRunning = false;

                VisualControlStatus.BackColor = Color.Red;
                VisualControlStatusLbl.Text = "Off";
                MenuUnitCelsius.Enabled = true;
                MenuUnitFarenheit.Enabled = true;
            }
        }

        private void MainForm_TimerTick(object sender,EventArgs e)
        {
            double tempSet; 

            TempLbl.Text = pidTask.TempRead.ToString("00.00");

            TimeStepsLbl.Text = pidTask.NumOfSteps.ToString("00");
            TimeToEqLbl.Text = (pidTask.NumOfSteps * double.Parse(TimeStepBx.Text)).ToString("000.00");

            ProportionalContributionLbl.Text = pidTask.ProportionalContribution.ToString("0.00");
            IntegralContributionLbl.Text = pidTask.IntegralContribution.ToString("0.00");
            DerivativeContributionLbl.Text = pidTask.DerivativeContribution.ToString("0.00");

            TempPlot.ChartAreas[0].AxisY.Minimum = pidTask.TempSet - 1.0;
            TempPlot.ChartAreas[0].AxisY.Maximum = pidTask.TempSet + 1.0;
            TempPlot.Series[0].Points.AddY(pidTask.TempRead);

            TempPlotExpanded.ChartAreas[0].AxisY.Minimum = pidTask.TempSet - 15.0;
            TempPlotExpanded.ChartAreas[0].AxisY.Maximum = pidTask.TempSet + 15.0;
            TempPlotExpanded.Series[0].Points.AddY(pidTask.TempRead);

            if (double.TryParse(TempSetBx.Text, out tempSet))
            {
                TempPlot.Series[1].Points.AddY(tempSet);
                TempPlotExpanded.Series[1].Points.AddY(tempSet);
            }
            else
            {
                TempPlot.Series[1].Points.AddY(0.0);
                TempPlotExpanded.Series[1].Points.AddY(0.0);
            }

        }
        private void PID_TimeStep(object sender, EventArgs e)
        {
            pidTask.Update();
        }

        #region Menus
        private void MenuLockTimeStepSize_Click(object sender, EventArgs e)
        {
            if (!TimeStepBx.Enabled)
            {
                TimeStepBx.Enabled = true;
                MenuLockTimeStepSize.Text = "Lock Timestep Size";
            }
            else
            {
                TimeStepBx.Enabled = false;
                MenuLockTimeStepSize.Text = "Unlock Timestep Size";
            }
        }
        private void MenuLockAParameter_Click(object sender, EventArgs e)
        {
            if (!ProportionalBx.Enabled)
            {
                ProportionalBx.Enabled = true;
                MenuLockAParameter.Text = "Lock A Parameter";
            }
            else
            {
                ProportionalBx.Enabled = false;
                MenuLockAParameter.Text = "Unlock A Parameter";
            }
        }
        private void MenuLockBParameter_Click(object sender, EventArgs e)
        {
            if (!IntegralBx.Enabled)
            {
                IntegralBx.Enabled = true;
                MenuLockBParameter.Text = "Lock B Parameter";
            }
            else
            {
                IntegralBx.Enabled = false;
                MenuLockBParameter.Text = "Unlock B Parameter";
            }
        }
        private void MenuLockCParameter_Click(object sender, EventArgs e)
        {
            if (!DerivativeBx.Enabled)
            {
                DerivativeBx.Enabled = true;
                MenuLockCParameter.Text = "Lock C Parameter";
            }
            else
            {
                DerivativeBx.Enabled = false;
                MenuLockCParameter.Text = "Unlock C Parameter";
            }
        }

        private void MenuUnitCelsius_Click(object sender, EventArgs e)
        {
            pidTask.Units = "Celsius";
            TempUnitLbl.Text = "°C";
            TsetLbl.Text = "T Set (°C):";
            tempUnits = "Celsius";

            TempPlot.ChartAreas[0].AxisY.Title = "Temperature (°C)";
            TempPlotExpanded.ChartAreas[0].AxisY.Title = "Temperature (°C)";
        }
        private void MenuUnitFarenheit_Click(object sender, EventArgs e)
        {
            pidTask.Units = "Farenheit";
            TempUnitLbl.Text = "°F";
            TsetLbl.Text = "T Set (°F):";
            tempUnits = "Farenheit";

            TempPlot.ChartAreas[0].AxisY.Title = "Temperature (°F)";
            TempPlotExpanded.ChartAreas[0].AxisY.Title = "Temperature (°F)";
        }

        private void MenuDeviceRefresh_Click(object sender, EventArgs e)
        {
            double tempSet,timeStep,proportionalTerm,integralTerm,derivativeTerm;
            bool devExists = false;

            DeviceBx.Items.Clear();
            VoltChnnlBx.Items.Clear();
            TempChnnlBx.Items.Clear();

            try
            {
                DeviceBx.Items.AddRange(DaqSystem.Local.Devices);
                VoltChnnlBx.Items.AddRange(DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.AO, PhysicalChannelAccess.External));
                TempChnnlBx.Items.AddRange(DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.AI, PhysicalChannelAccess.External));

                if (!double.TryParse(TempSetBx.Text, out tempSet))
                {
                    tempSet = 22.0;
                    TempSetBx.Text = "22.0";
                }
                if (!double.TryParse(TimeStepBx.Text, out timeStep))
                {
                    timeStep = 0.01;
                    TimeStepBx.Text = "0.01";
                }
                if (!double.TryParse(ProportionalBx.Text, out proportionalTerm))
                {
                    proportionalTerm = 0.0;
                    ProportionalBx.Text = "0.0";
                }
                if (!double.TryParse(IntegralBx.Text, out integralTerm))
                {
                    integralTerm = 0.0;
                    IntegralBx.Text = "0.0";
                }
                if (!double.TryParse(DerivativeBx.Text, out derivativeTerm))
                {
                    derivativeTerm = 0.0;
                    DerivativeBx.Text = "0.0";
                }

                if (DeviceBx.Items.Count != 0)
                {
                    pidTask = new PID(DeviceBx.Items[0].ToString(), 0, 0, AITerminalConfiguration.Rse){
                        TimeStep = timeStep,
                        TempSet = tempSet,
                        ProportionalTerm = proportionalTerm,
                        Integralterm = integralTerm,
                        DerivativeTerm = derivativeTerm,
                        Units = tempUnits
                    };
                    devExists = true;
                }
                else
                {
                    pidTask = new PID(DeviceBx.Text, 0, 0, AITerminalConfiguration.Rse){
                        TimeStep = timeStep,
                        TempSet = tempSet,
                        ProportionalTerm = proportionalTerm,
                        Integralterm = integralTerm,
                        DerivativeTerm = derivativeTerm,
                        Units = tempUnits
                    };
                    devExists = false;
                }
            }
            catch (DaqException)
            {
                TempSetBx.Enabled = false;
                TimeStepBx.Enabled = false;
                ProportionalBx.Enabled = false;
                IntegralBx.Enabled = false;
                DerivativeBx.Enabled = false;
                StartStopBttn.Enabled = false;
                MessageBox.Show("No DAQ device is present. Please check device connection in NImax and refresh the device list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (DeviceBx.Items.Count != 0)
                {
                    DeviceBx.SelectedIndex = 0;
                    VoltChnnlBx.SelectedIndex = 0;
                    TempChnnlBx.SelectedIndex = 0;
                }
                else
                {
                    DeviceBx.SelectedIndex = -1;
                    VoltChnnlBx.SelectedIndex = -1;
                    TempChnnlBx.SelectedIndex = -1;
                }

                if(devExists)
                {
                    TempSetBx.Enabled = true;
                    TimeStepBx.Enabled = true;
                    ProportionalBx.Enabled = true;
                    IntegralBx.Enabled = true;
                    DerivativeBx.Enabled = true;
                    StartStopBttn.Enabled = true;
                }
            }
        }

        private void MenuProgramHelp_Click(object sender, EventArgs e)
        {
            string readmedirectory = AppDomain.CurrentDomain.BaseDirectory;
            string readmefilepath = Path.Combine(readmedirectory, "README.txt");

            try
            {
                Help.ShowHelp(null, readmefilepath);
            }
            catch
            {
                MessageBox.Show("Could not find README file. Make sure file is located in bin folder of application directory, or check that the filename has not been changed.");
            }
            
        }
        private void MenuDisplaysHelp_Click(object sender, EventArgs e)
        {
            string readmedirectory = AppDomain.CurrentDomain.BaseDirectory;
            string readmefilepath = Path.Combine(readmedirectory, "PIDDisplayREADME.txt");

            try
            {
                Help.ShowHelp(null, readmefilepath);
            }
            catch
            {
                MessageBox.Show("Could not find README file. Make sure file is located in bin folder of application directory, or check that the filename has not been changed.");
            }

        }
        #endregion

    }
}
