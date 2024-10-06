﻿using MainCore.Tasks;
using MainCore.UI.Enums;
using MainCore.UI.Models.Output;
using MainCore.UI.Stores;
using MainCore.UI.ViewModels.Abstract;
using MainCore.UI.ViewModels.UserControls;
using ReactiveUI;
using System.Reactive.Linq;

namespace MainCore.UI.ViewModels.Tabs
{
    [RegisterSingleton(Registration = RegistrationStrategy.Self)]
    public class VillageViewModel : AccountTabViewModelBase
    {
        private readonly VillageTabStore _villageTabStore;

        private readonly ITaskManager _taskManager;
        private readonly IDialogService _dialogService;
        private readonly GetVillage _getVillage;
        public ListBoxItemViewModel Villages { get; } = new();

        public VillageTabStore VillageTabStore => _villageTabStore;
        public ReactiveCommand<Unit, Unit> LoadCurrent { get; }
        public ReactiveCommand<Unit, Unit> LoadUnload { get; }
        public ReactiveCommand<Unit, Unit> LoadAll { get; }
        public ReactiveCommand<AccountId, List<ListBoxItem>> LoadVillage { get; }

        public VillageViewModel(VillageTabStore villageTabStore, ITaskManager taskManager, IDialogService dialogService, GetVillage getVillage)
        {
            _villageTabStore = villageTabStore;
            _taskManager = taskManager;
            _dialogService = dialogService;
            _getVillage = getVillage;

            LoadCurrent = ReactiveCommand.CreateFromTask(LoadCurrentHandler);
            LoadUnload = ReactiveCommand.CreateFromTask(LoadUnloadHandler);
            LoadAll = ReactiveCommand.CreateFromTask(LoadAllHandler);
            LoadVillage = ReactiveCommand.Create<AccountId, List<ListBoxItem>>(LoadVillageHandler);

            var villageObservable = this.WhenAnyValue(x => x.Villages.SelectedItem);
            villageObservable.BindTo(_selectedItemStore, vm => vm.Village);
            villageObservable.Subscribe(x =>
            {
                var tabType = VillageTabType.Normal;
                if (x is null) tabType = VillageTabType.NoVillage;
                _villageTabStore.SetTabType(tabType);
            });

            LoadVillage.Subscribe(Villages.Load);
        }

        public async Task VillageListRefresh(AccountId accountId)
        {
            if (!IsActive) return;
            if (accountId != AccountId) return;
            await LoadVillage.Execute(accountId);
        }

        protected override async Task Load(AccountId accountId)
        {
            await LoadVillage.Execute(accountId);
        }

        private async Task LoadCurrentHandler()
        {
            if (!Villages.IsSelected)
            {
                _dialogService.ShowMessageBox("Warning", "No village selected");
                return;
            }

            var villageId = new VillageId(Villages.SelectedItemId);
            await _taskManager.AddOrUpdate<UpdateBuildingTask>(AccountId, villageId);

            _dialogService.ShowMessageBox("Information", $"Added update task");
        }

        private async Task LoadUnloadHandler()
        {
            var villages = _getVillage.Missing(AccountId);
            foreach (var village in villages)
            {
                await _taskManager.AddOrUpdate<UpdateBuildingTask>(AccountId, village);
            }
            _dialogService.ShowMessageBox("Information", $"Added update task");
        }

        private async Task LoadAllHandler()
        {
            var villages = _getVillage.All(AccountId);
            foreach (var village in villages)
            {
                await _taskManager.AddOrUpdate<UpdateBuildingTask>(AccountId, village);
            }
            _dialogService.ShowMessageBox("Information", $"Added update task");
        }

        private List<ListBoxItem> LoadVillageHandler(AccountId accountId)
        {
            return _getVillage.Info(accountId);
        }
    }
}