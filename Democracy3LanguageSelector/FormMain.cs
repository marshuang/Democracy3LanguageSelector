﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Democracy3LanguageSelector
{
    public partial class FormMain : Form
    {
        public Project ProjectInfo { get; set; }
        public Dictionary<string, bool> DownloadingResources { get; set; }

        public FormMain()
        {
            InitializeComponent();

            this.DownloadingResources = new Dictionary<string, bool>();
            this.comboBoxLanguages.SelectedIndex = 0;
            this.comboBoxLanguages.Enabled = false;

            // Create cache folder
            this.CreateCacheFolderIfNOtExist();

            // Chargement
            LoadLanguagesList();
            LoadGamePath();
        }

        private void buttonApply_Click(object sender, EventArgs e)
        {
            if (comboBoxLanguages.SelectedItem is Language)
            {
                if (!Directory.Exists(this.labelGameSourcePath.Text))
                {
                    MessageBox.Show("Please select a valid game path...", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    // list all file from lang cache folder
                    var langCode = ((Language)comboBoxLanguages.SelectedItem).Code;

                    var files = Directory.EnumerateFiles(@"cache\{0}\".FormatWith(langCode));

                    var gameInjector = new DemocracyStringHandling(null, null, this.labelGameSourcePath.Text);

                    // For each file extract and inject
                    foreach (var file in files)
                    {
                        gameInjector.TransifexFilePath = file;
                        gameInjector.InjectTransifexFile();
                    }

                    var dialogResult = MessageBox.Show("Game is now translated in {0}\r\nLaunch game ?".FormatWith(((Language)comboBoxLanguages.SelectedItem).Name), "Translation successful...", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                    {
                        var exePath = this.labelGameSourcePath.Text.Replace("\\data", "") + "\\Democracy3.exe";
                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = exePath;
                        psi.WorkingDirectory = System.IO.Path.GetDirectoryName(psi.FileName);
                        System.Diagnostics.Process.Start(psi);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private async void LoadLanguagesList()
        {
            var transifexConnector = new TransifexConnector();
            this.ProjectInfo = await transifexConnector.ProjectDetails();

            this.ProjectInfo.Teams.ForEach(t => this.DownloadingResources.Add(t, false));

            this.CreateLangueCacheFolderIfNOtExist(this.ProjectInfo.Teams);
            this.comboBoxLanguages.DataSource = new BindingSource(this.ProjectInfo.Languages, null);
            this.comboBoxLanguages.DisplayMember = "Name";
            this.comboBoxLanguages.ValueMember = "Code";
            this.comboBoxLanguages.Text = string.Empty;
            this.comboBoxLanguages.Enabled = true;
        }

        private void LoadGamePath()
        {
            string InstallPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\GOG.com\GOGDEMOCRACY3", "PATH", null);
            if (!string.IsNullOrEmpty(InstallPath))
                this.labelGameSourcePath.Text = InstallPath + "data";
        }

        private async void comboBoxLanguages_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.buttonApply.Visible = false;

            if (comboBoxLanguages.SelectedItem is Language)
            {
                this.labelProgression.Text = "Loading transifex translations files...";
                var langCode = ((Language)comboBoxLanguages.SelectedItem).Code;
                var connector = new TransifexConnector();
                var langDetails = await connector.TranslationDetails(langCode);

                // Download and Cache files
                if (!this.DownloadingResources[langCode])
                {
                    this.DownloadingResources[langCode] = true;

                    // Calculate file size for progress
                    this.progressBarDownlodResources.Visible = true;
                    this.progressBarDownlodResources.Value = 0;
                    this.progressBarDownlodResources.Maximum = await connector.GetTotalResouresSize(langCode, this.ProjectInfo.Resources);
                    this.progressBarDownlodResources.Step = 1024;

                    // Progress reporter
                    var progress = new Progress<int>();
                    progress.ProgressChanged += (s, percent) => { 
                        progressBarDownlodResources.PerformStep(); 
                    };

                    // Download file
                    await connector.DownloadTranslationResources(langCode, this.ProjectInfo.Resources, progress);

                    this.DownloadingResources[langCode] = false;
                    this.buttonApply.Visible = true;
                    this.progressBarDownlodResources.Visible = false;

                    this.labelProgression.Text = langDetails.PercentProgression.ToString() + " %";
                    this.labelResourcesInfo.Text = ProjectInfo.Resources.Count + " files and " + langDetails.Translated_segments.ToString() + "/" + langDetails.Total_segments.ToString() + " translated sentences";
                }
            }
        }

        void progress_ProgressChanged(object sender, int e)
        {
            throw new NotImplementedException();
        }

        private void CreateCacheFolderIfNOtExist()
        {
            if (!Directory.Exists("cache"))
            {
                Directory.CreateDirectory("cache");
            }
        }

        private void CreateLangueCacheFolderIfNOtExist(List<string> languagesCode)
        {
            foreach (var code in languagesCode)
            {
                if (!Directory.Exists("cache\\" + code))
                    Directory.CreateDirectory("cache\\" + code);
            }
        }

        private void linkLabelGamePath_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.folderBrowserDialogGameSource.SelectedPath = this.labelGameSourcePath.Text;

            var result = this.folderBrowserDialogGameSource.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                this.labelGameSourcePath.Text = this.folderBrowserDialogGameSource.SelectedPath;
            }
        }
    }
}