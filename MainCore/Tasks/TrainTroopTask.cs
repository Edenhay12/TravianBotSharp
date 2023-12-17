﻿using FluentResults;
using MainCore.Commands;
using MainCore.Commands.Features;
using MainCore.Common.Enums;
using MainCore.Common.Errors.TrainTroop;
using MainCore.Infrasturecture.AutoRegisterDi;
using MainCore.Repositories;
using MainCore.Tasks.Base;
using MediatR;

namespace MainCore.Tasks
{
    [RegisterAsTransient(withoutInterface: true)]
    public class TrainTroopTask : VillageTask
    {
        private static readonly Dictionary<BuildingEnums, VillageSettingEnums> _settings = new()
        {
            {BuildingEnums.Barracks, VillageSettingEnums.BarrackTroop },
            {BuildingEnums.Stable, VillageSettingEnums.StableTroop },
            {BuildingEnums.Workshop, VillageSettingEnums.WorkshopTroop },
        };

        public TrainTroopTask(UnitOfCommand unitOfCommand, UnitOfRepository unitOfRepository, IMediator mediator) : base(unitOfCommand, unitOfRepository, mediator)
        {
        }

        protected override async Task<Result> Execute()
        {
            var buildings = _unitOfRepository.BuildingRepository.GetTrainTroopBuilding(VillageId);
            if (buildings.Count == 0) return Result.Ok();

            Result result;
            var settings = new Dictionary<VillageSettingEnums, int>();
            foreach (var building in buildings)
            {
                result = await _mediator.Send(new TrainTroopCommand(AccountId, VillageId, building));
                if (result.IsFailed)
                {
                    if (result.HasError<MissingBuilding>())
                    {
                        settings.Add(_settings[building], 0);
                    }
                    else if (result.HasError<MissingResource>())
                    {
                        break;
                    }
                }
            }

            _unitOfRepository.VillageSettingRepository.Update(VillageId, settings);
            SetNextExecute();
            return Result.Ok();
        }

        private void SetNextExecute()
        {
            var seconds = _unitOfRepository.VillageSettingRepository.GetByName(VillageId, VillageSettingEnums.TrainTroopRepeatTimeMin, VillageSettingEnums.TrainTroopRepeatTimeMax, 60);
            ExecuteAt = DateTime.Now.AddSeconds(seconds);
        }

        protected override void SetName()
        {
            var name = _unitOfRepository.VillageRepository.GetVillageName(VillageId);
            _name = $"Training troop in {name}";
        }
    }
}