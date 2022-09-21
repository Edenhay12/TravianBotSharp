﻿using MainCore.Helper;
using MainCore.Tasks.Sim;
using Microsoft.Win32;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using WPFUI.Interfaces;
using WPFUI.Models;
using WPFUI.ViewModels.Abstract;

namespace WPFUI.ViewModels.Tabs.Villages
{
    public class SettingsViewModel : VillageTabBaseViewModel, IVillageTabPage
    {
        public SettingsViewModel()
        {
            SaveCommand = ReactiveCommand.CreateFromTask(SaveTask);
            ExportCommand = ReactiveCommand.Create(ExportTask);
            ImportCommand = ReactiveCommand.Create(ImportTask);
        }

        public void OnActived()
        {
            LoadData(VillageId);
        }

        protected override void LoadData(int index)
        {
            using var context = _contextFactory.CreateDbContext();
            if (context.Villages.Find(index) is null) return;

            var settings = context.VillagesSettings.Find(index);
            Settings.CopyFrom(settings);
        }

        private async Task SaveTask()
        {
            if (!CheckInput()) return;
            _waitingWindow.ViewModel.Show("saving village's settings");

            await Observable.Start(() =>
            {
                Save(VillageId);
                TaskBasedSetting(VillageId, AccountId);
            }, RxApp.TaskpoolScheduler);
            _waitingWindow.ViewModel.Close();

            MessageBox.Show("Saved.");
        }

        private void ImportTask()
        {
            if (!CheckInput()) return;

            using var context = _contextFactory.CreateDbContext();
            var village = context.Villages.Find(VillageId);
            var ofd = new OpenFileDialog
            {
                InitialDirectory = AppContext.BaseDirectory,
                Filter = "TBS files (*.tbs)|*.tbs|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                FileName = $"{village.Name}_settings.tbs",
            };

            if (ofd.ShowDialog() == true)
            {
                var jsonString = File.ReadAllText(ofd.FileName);
                try
                {
                    var setting = JsonSerializer.Deserialize<MainCore.Models.Database.VillageSetting>(jsonString);
                    setting.VillageId = VillageId;
                    context.Update(setting);
                    context.SaveChanges();
                    LoadData(VillageId);
                    TaskBasedSetting(VillageId, AccountId);
                }
                catch
                {
                    MessageBox.Show("Invalid file.", "Warning");
                }
            }
        }

        private void ExportTask()
        {
            using var context = _contextFactory.CreateDbContext();
            var setting = context.VillagesSettings.Find(VillageId);
            var jsonString = JsonSerializer.Serialize(setting);
            var village = context.Villages.Find(VillageId);
            var svd = new SaveFileDialog
            {
                InitialDirectory = AppContext.BaseDirectory,
                Filter = "TBS files (*.tbs)|*.tbs|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                FileName = $"{village.Name}_settings.tbs",
            };

            if (svd.ShowDialog() == true)
            {
                File.WriteAllText(svd.FileName, jsonString);
            }
        }

        private bool CheckInput()
        {
            if (!Settings.InstantCompleteTime.IsNumeric())
            {
                MessageBox.Show("Instant complete time is non-numeric.", "Warning");
                return false;
            }
            if (!Settings.AdsUpgradeTime.IsNumeric())
            {
                MessageBox.Show("Ads upgrade time is non-numeric.", "Warning");
                return false;
            }
            return true;
        }

        private void Save(int index)
        {
            using var context = _contextFactory.CreateDbContext();
            var setting = context.VillagesSettings.Find(index);
            Settings.CopyTo(setting);
            context.Update(setting);
            context.SaveChanges();
        }

        private void TaskBasedSetting(int villageId, int accountId)
        {
            var list = _taskManager.GetList(accountId);
            var tasks = list.Where(x => x.GetType() == typeof(InstantUpgrade));
            if (Settings.IsInstantComplete)
            {
                if (!tasks.Any())
                {
                    using var context = _contextFactory.CreateDbContext();
                    var currentBuildings = context.VillagesCurrentlyBuildings.Where(x => x.VillageId == villageId).ToList();
                    var count = currentBuildings.Count(x => x.Level != -1);
                    if (count > 0)
                    {
                        _taskManager.Add(accountId, new InstantUpgrade(villageId, accountId));
                    }
                }
            }
            else
            {
                foreach (var item in tasks)
                {
                    _taskManager.Remove(accountId, item);
                }
            }
        }

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportCommand { get; }
        public ReactiveCommand<Unit, Unit> ImportCommand { get; }

        public VillageSetting Settings { get; } = new();
    }
}