﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Config.NET;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace FileMerger
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }
        private System.Timers.Timer _timer;





        #region Controls
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show(@"Are you sure that you want to exit?", @"Exit?",
                       MessageBoxButtons.YesNo,
                       MessageBoxIcon.Information) == DialogResult.No)
            {
                e.Cancel = true;
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                var file = new ConfigFile("", "fmSettings");
                file.AddConfig("paths", GetPathsFromDictionary());
                file.AddConfig("fileOutput", txtFileOutput.Text);
                file.AddConfig("runOnStart", cbWatchOnStartUp.Checked.ToString());
                file.AddConfig("minimizeToTray", cbSystemTray.Checked.ToString());
                file.AddConfig("fileOutputFormat", rtxtFormat.Text);
                file.AddConfig("refreshInterval", nudSeconds.Value.ToString());
                file.Write();
                LogMessage("Settings saved");
                if (MessageBox.Show(@"Restart required to apply new settings, restart?", @"Restart?",
                   MessageBoxButtons.YesNo,
                   MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Application.Restart();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to save. Error: {ex}");
                MessageBox.Show($"Failed to save. Error: {ex}");

            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            try
            {
                this.Text = $@"FileMerger (C) SharpDevs 2016";
                var file = new ConfigFile("", "fmSettings");
                if (file.FileExists())
                {
                    LogMessage("Loading settings..");
                    file.Load();
                    FileMergerSettings.FileOutput = file.GetConfig("fileOutput");
                    FileMergerSettings.StartOnStartup = Convert.ToBoolean(file.GetConfig("runOnStart"));
                    FileMergerSettings.MinimizeToTray = Convert.ToBoolean(file.GetConfig("minimizeToTray"));
                    FileMergerSettings.OutputFormat = file.GetConfig("fileOutputFormat");
                    FileMergerSettings.RefreshInterval = (double)Convert.ToInt32(file.GetConfig("refreshInterval"));
                    FileMergerSettings.Files = LoadFilesFromSetting(file.GetConfig("paths"));
                    PopulateGrid();
                    txtFileOutput.Text = FileMergerSettings.FileOutput;
                    cbWatchOnStartUp.Checked = FileMergerSettings.StartOnStartup;
                    cbSystemTray.Checked = FileMergerSettings.MinimizeToTray;
                    rtxtFormat.Text = FileMergerSettings.OutputFormat;
                    nudSeconds.Value = (decimal)FileMergerSettings.RefreshInterval;
                    ReLoadCache();
                    btnStart.Enabled = true;
                    if (FileMergerSettings.StartOnStartup)
                    {
                        StartTimers();
                        gbFiles.Enabled = false;
                        gbSettings.Enabled = false;
                        btnStart.Enabled = false;
                        btnExit.Enabled = false;
                        btnSave.Enabled = false;
                        rtxtFormat.Enabled = false;
                        btnStop.Enabled = true;
                    }
                    else
                    {
                        btnStop.Enabled = false;
                        btnStart.Enabled = true;
                    }
                    LogMessage("Done loading settings.");
                }
                else
                {
                    btnStop.Enabled = false;
                    btnStart.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to load Error: {ex.ToString()}");
                MessageBox.Show($"Failed to load Error: {ex.ToString()}");
            }
        }



        private void btnFileOutput_Click(object sender, EventArgs e)
        {
            txtFileOutput.Text = GetFilePath();
        }
        private void btnClearConfig_Click(object sender, EventArgs e)
        {
            try
            {
                var file = new ConfigFile("", "fmSettings");
                file.Delete();
                if (MessageBox.Show(@"Restart required, restart?", @"Restart?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Application.Restart();
                }

            }
            catch (Exception ex)
            {
                LogMessage($"Failed to clear config. Error: {ex.ToString()}");
                MessageBox.Show($"Failed to clear config. Error: {ex.ToString()}");
            }
        }
        private void MainForm_Resize(object sender, EventArgs e)
        {

            notifyIcon1.BalloonTipTitle = @"FileMerger";
            notifyIcon1.BalloonTipText = @"FileMerger is still running in the background. Click the system tray to get me back.";

            if (WindowState == FormWindowState.Minimized)
            {
                if (!FileMergerSettings.MinimizeToTray) return;
                notifyIcon1.Visible = true;
                notifyIcon1.ShowBalloonTip(500);
                Hide();
            }
            else if (WindowState == FormWindowState.Normal)
            {
                notifyIcon1.Visible = false;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
        }
        private void btnMergeFilesNow_Click(object sender, EventArgs e)
        {
            LogMessage("Merging files");
            WriteFile();
            LogMessage("Done merging files");
        }
        private void btnStart_Click(object sender, EventArgs e)
        {
            StartTimers();
            gbFiles.Enabled = false;
            gbSettings.Enabled = false;
            btnStart.Enabled = false;
            btnExit.Enabled = false;
            btnSave.Enabled = false;
            rtxtFormat.Enabled = false;
            btnStop.Enabled = true;
        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            _timer.Stop();
            gbFiles.Enabled = true;
            gbSettings.Enabled = true;
            btnStart.Enabled = true;
            btnExit.Enabled = true;
            btnSave.Enabled = true;
            rtxtFormat.Enabled = true;
            btnStop.Enabled = false;
        }
        private void btnWriteLog_Click(object sender, EventArgs e)
        {
            File.WriteAllLines("FileMergerLog.txt", rtxtLog.Lines);
        }

        private void btnAddFile_Click(object sender, EventArgs e)
        {
            var dialog = new AddFile();
            var result = dialog.ShowDialog();
            if (result != DialogResult.OK) return;
            object[] file = { dialog.Key, dialog.Path };
            FileMergerSettings.Files.Add(dialog.Key, dialog.Path);
            dgvFiles.Rows.Add(file);
        }
        private void btnRemoveSelected_Click(object sender, EventArgs e)
        {
            if (dgvFiles.Rows.Count <= 0) return;
            if (!dgvFiles.SelectedRows[0].IsNewRow)
                FileMergerSettings.Files.Remove(dgvFiles.SelectedRows[0].Cells["colKey"].Value.ToString());
            FileMergerSettings.FileCache.Remove(dgvFiles.SelectedRows[0].Cells["colKey"].Value.ToString());
            dgvFiles.Rows.RemoveAt(dgvFiles.SelectedRows[0].Index);
        }

        private void btnCopyKey_Click(object sender, EventArgs e)
        {
            if (dgvFiles.Rows.Count <= 0) return;
            Clipboard.SetText(dgvFiles.SelectedRows[0].Cells["colKey"].Value.ToString());
        }
        #endregion

        #region Reading
        private string ReadFile(string filePath)
        {
            try
            {
                var sendBack = "";
                using (var reader = new StreamReader(filePath))
                {
                    sendBack = reader.ReadToEnd();
                }
                return sendBack;
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to read file. {ex.ToString()}");
                return "";
            }

        }

        #endregion

        #region Writing
        private void WriteFile()
        {
            try
            {
                var outputFormat = FileMergerSettings.OutputFormat;
                var outFile = FileMergerSettings.FileOutput;
                outputFormat = FileMergerSettings.FileCache.Aggregate(outputFormat, (current, file) => Regex.Replace(current, file.Key, file.Value));
                using (var writer = new StreamWriter(outFile, false))
                {
                    writer.Write(outputFormat);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to write to file. {ex}");
            }
        }
        #endregion

        #region Loading / Reloading

        private Dictionary<string, string> LoadFilesFromSetting(string input)
        {
            var paths = input.Split(',');
            return paths.Select(path => path.Split(';')).ToDictionary(parts => parts[0], parts => parts[1]);
        }
        private void ReLoadCache()
        {
            FileMergerSettings.FileCache.Clear();
            foreach (var file in FileMergerSettings.Files)
            {
                FileMergerSettings.FileCache.Add(file.Key, ReadFile(file.Value));
            }

        }

        private void PopulateGrid()
        {
            foreach (var file in FileMergerSettings.Files)
            {
                object[] obj = { file.Key, file.Value };
                dgvFiles.Rows.Add(obj);
            }

        }
        #endregion

        #region Utils
        private static string GetPathsFromDictionary()
        {
            return string.Join(",", FileMergerSettings.Files.Select(x => x.Key + ";" + x.Value).ToArray());
        }
        private string GetFilePath()
        {
            try
            {
                var ofd = new OpenFileDialog
                {
                    Filter = @"Textfiles|*.txt",
                    Multiselect = false,
                    CheckFileExists = true
                };
                var result = ofd.ShowDialog();
                return result == DialogResult.OK ? ofd.FileName : "";

            }
            catch (Exception ex)
            {
                LogMessage($"Failed to get path Error: {ex.ToString()}");
                MessageBox.Show($"Failed to get path Error: {ex.ToString()}");
                return "";
            }
        }
        private void LogMessage(string message)
        {
            Invoke((MethodInvoker)delegate
            {
                var log = rtxtLog.Text;
                var newText = $"[{DateTime.Now}] " + message + Environment.NewLine + log;
                rtxtLog.Clear();
                rtxtLog.Text = newText;
            });
        }
        #endregion

        #region Timers
        private void StartTimers()
        {
            var interval = (int)TimeSpan.FromSeconds((double)FileMergerSettings.RefreshInterval).TotalMilliseconds;
            var one = new System.Timers.Timer { Interval = interval };
            one.Elapsed += One_Elapsed;
            _timer = one;
            one.Start();
        }

        private void One_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                LogMessage("Timer elapsed, updating file..");
                ReLoadCache();
                WriteFile();
                LogMessage("Done updating file..");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to updaste. Error: {ex.ToString()}");
            }
        }
        #endregion
    }
}

