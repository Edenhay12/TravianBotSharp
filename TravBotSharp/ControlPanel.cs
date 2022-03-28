﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Reflection;
using System.Windows.Forms;
using TbsCore.Database;
using TbsCore.Helpers;
using TbsCore.Models.AccModels;

using TravBotSharp.Forms;
using TravBotSharp.Interfaces;
using TravBotSharp.Views;
using TbsCore.Models.Logging;
using TbsCore.Models.VillageModels;

namespace TravBotSharp
{
    public partial class ControlPanel : Form
    {
        private List<Account> accounts = new List<Account>();
        private int accSelected = 0;
        private System.Timers.Timer saveAccountsTimer;
        private ITbsUc[] Ucs;

        public ControlPanel()
        {
            InitializeComponent();
            //read list of accounts!
            SerilogSingleton.Init();

            LoadAccounts();
            accListView.Select();

            // Be sure to have these in correct order!
            Ucs = new ITbsUc[]
            {
                generalUc1,
                heroUc1,
                villagesUc1,
                overviewUc1,
                overviewTroopsUc1,
                farmingUc1,
                newVillagesUc1,
                deffendingUc1,
                questsUc1,
                discordUc1,
                debugUc1,
            };

            // Initialize all the views
            foreach (var uc in Ucs) uc.Init(this);

            saveAccountsTimer = new System.Timers.Timer(1000 * 60 * 30); // Every 30 min
            saveAccountsTimer.Elapsed += SaveAccounts_TimerElapsed;
            saveAccountsTimer.AutoReset = true;
            saveAccountsTimer.Start();

            // So TbsCore can access forms and alert user
            IoHelperCore.AlertUser = IoHelperForms.AlertUser;

            checkNewVersion();
            this.debugUc1.InitLog(LogOutput.Instance);
            UseragentDatabase.Instance.Load();
        }

        private void SaveAccounts_TimerElapsed(object sender, ElapsedEventArgs e) => IoHelperCore.SaveAccounts(accounts, false);

        private void LoadAccounts()
        {
            accounts = DbRepository.GetAccounts();

            accounts.ForEach(x =>
            {
                ObjectHelper.FixAccObj(x, x);
                x.Tasks = new TaskList(x);
                x.TaskTimer = new TaskTimer(x);
                // we will check again before we login
                x.Access.AllAccess.ForEach(a => a.Ok = true);

                LogOutput.Instance.AddUsername(x.AccInfo.Nickname);
                x.Logger = new Logger(x.AccInfo.Nickname);

                x.Villages.ForEach(vill => vill.UnfinishedTasks = new List<VillUnfinishedTask>());
                // x.Tasks.Load();
            });

            RefreshAccView();
        }

        private void button1_Click(object sender, EventArgs e) // Add account
        {
            using (var form = new AddAccount())
            {
                form.UpdateWindow();
                var result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    var acc = form.Acc;
                    DbRepository.SaveAccount(acc);
                    if (string.IsNullOrEmpty(acc.AccInfo.Nickname) ||
                        string.IsNullOrEmpty(acc.AccInfo.ServerUrl)) return;

                    LogOutput.Instance.AddUsername(acc.AccInfo.Nickname);
                    acc.Logger = new Logger(acc.AccInfo.Nickname);

                    acc.Villages.ForEach(vill => vill.UnfinishedTasks = new List<VillUnfinishedTask>());
                    accounts.Add(acc);
                    RefreshAccView();
                }
            }
        }

        private void ControlPanel_FormClosing(object sender, FormClosingEventArgs e)
        {
            IoHelperCore.SaveAccounts(accounts, true);
            SerilogSingleton.Close();
        }

        /// <summary>
        /// Refreshes the account view. Account currently selected will be colored in blue.
        /// </summary>
        public void RefreshAccView()
        {
            accListView.Items.Clear();
            for (int i = 0; i < accounts.Count; i++)
            {
                var access = accounts[i].Access.GetCurrentAccess();
                InsertAccIntoListView(accounts[i].AccInfo.Nickname,
                    accounts[i].AccInfo.ServerUrl,
                    access?.Proxy ?? "NO ACCESS",
                    access?.ProxyPort ?? 0,
                    i == accSelected);
            }
        }

        private void InsertAccIntoListView(string nick, string url, string proxy, int port, bool selected = false)
        {
            var item = new ListViewItem();
            item.SubItems[0].Text = $"{nick} ({IoHelperCore.UrlRemoveHttp(url)})"; //account
            item.SubItems[0].ForeColor = Color.FromName(selected ? "DodgerBlue" : "Black");
            //item.SubItems.Add("❌"); //proxy error
            item.SubItems.Add(string.IsNullOrEmpty(proxy) ? "/" : proxy + ":" + port); //proxy
            accListView.Items.Add(item);
        }

        private async void button2_Click(object sender, EventArgs e) //login button
        {
            var acc = GetSelectedAcc();
            if (0 < acc.Access.AllAccess.Count)
            {
                var task = await Task.Run(async () =>
                {
                    var success = await IoHelperCore.LoginAccount(acc);
                    if (!success) return false;
                    acc.Tasks.OnUpdateTask = debugUc1.UpdateTaskTable;
                    debugUc1.UpdateTaskTable();

                    return true;
                });

                if (task)
                {
                    generalUc1.UpdateBotRunning("true");
                    return;
                }
                else
                {
                    _ = MessageBox.Show("Check debug log to more info", "Error in account", MessageBoxButtons.OK);
                    return;
                }
            }

            // Alert user that account has no access defined
            string message = "Account you are trying to login has no access' defined. Please edit the account.";
            string caption = "Error in account";
            MessageBoxButtons buttons = MessageBoxButtons.OK;
            DialogResult result = MessageBox.Show(message, caption, buttons);
        }

        private void button3_Click(object sender, EventArgs e) // Remove an account
        {
            var acc = GetSelectedAcc();
            if (acc == null) return;

            IoHelperCore.RemoveCache(acc);
            accounts.Remove(acc);
            DbRepository.RemoveAccount(acc);
            accListView.Items.RemoveAt(accSelected);
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateFrontEnd();
        }

        private void accListView_SelectedIndexChanged(object sender, EventArgs e) // Different acc selected
        {
            // remove event task update on previous account
            if (GetSelectedAcc().Tasks != null) GetSelectedAcc().Tasks.OnUpdateTask = null;

            var indicies = accListView.SelectedIndices;
            if (indicies.Count > 0)
            {
                accSelected = indicies[0];
            }
            var acc = GetSelectedAcc();
            // If account has no Wb object, it's not logged in at the moment
            button2.Enabled = !(acc?.TaskTimer?.IsBotRunning ?? false);

            UpdateFrontEnd();

            foreach (ListViewItem item in accListView.Items)
            {
                item.SubItems[0].ForeColor = Color.FromName("Black");
            }
            accListView.Items[accSelected].SubItems[0].ForeColor = Color.FromName("DodgerBlue");

            if (acc.Tasks != null)
            {
                acc.Tasks.OnUpdateTask = debugUc1.UpdateTaskTable;
                debugUc1.UpdateTaskTable();
            }
        }

        private void UpdateFrontEnd()
        {
            var acc = GetSelectedAcc();
            if (acc == null) return;

            // Refresh data in this tab!
            Ucs.ElementAtOrDefault(accTabController.SelectedIndex)?.UpdateUc();
        }

        private void button7_Click(object sender, EventArgs e) // Edit an account
        {
            var acc = GetSelectedAcc();
            if (acc != null)
            {
                using (var form = new AddAccount(acc))
                {
                    form.UpdateWindow();
                    DbRepository.SaveAccount(acc);
                    var result = form.ShowDialog();
                    if (result != DialogResult.OK)
                    {
                        // TODO: log
                    }
                }
            }
        }

        public Account GetSelectedAcc()
        {
            try
            {
                if (accounts.Count <= accSelected) return accounts.FirstOrDefault();
                return accounts[accSelected];
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void button5_Click(object sender, EventArgs e) // Logout
        {
            new Thread(() => IoHelperCore.Logout(GetSelectedAcc())).Start();
            generalUc1.UpdateBotRunning("false");
        }

        private void button6_Click(object sender, EventArgs e) // Login all accounts
        {
            new Thread(async () =>
            {
                var ran = new Random();
                foreach (var acc in accounts)
                {
                    // If account is already running, don't login
                    if (acc.TaskTimer.IsBotRunning) continue;

                    _ = IoHelperCore.LoginAccount(acc);
                    await Task.Delay(AccountHelper.Delay(acc));
                }
            }).Start();
            generalUc1.UpdateBotRunning("true");
        }

        private void button4_Click(object sender, EventArgs e) // Logout all accounts
        {
            new Thread(() =>
            {
                foreach (var acc in accounts)
                {
                    IoHelperCore.Logout(acc);
                }
            }).Start();
            generalUc1.UpdateBotRunning("false");
        }

        private void checkNewVersion()
        {
            new Thread(async () =>
            {
                var result = await Task.WhenAll(GithubHelper.CheckGitHubLatestVersion(), GithubHelper.CheckGitHublatestBuild());
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                currentVersion = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build);
                var isNewAvailable = result[0] != null && (currentVersion.CompareTo(result[0]) < 0);

                if (isNewAvailable)
                {
                    using (var form = new NewRelease())
                    {
                        form.IsNewVersion = isNewAvailable;

                        form.LatestVersion = result[0] ?? currentVersion;
                        form.LatestBuild = result[1] ?? currentVersion;
                        form.CurrentVersion = currentVersion;

                        _ = form.ShowDialog();
                    }
                }
            }).Start();
        }
    }
}