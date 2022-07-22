﻿using MainCore.Models.Runtime;
using MainCore.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if TRAVIAN_OFFICIAL

using TravianOfficialCore.Parsers;

#elif TRAVIAN_OFFICIAL_HEROUI

using TravianOfficialNewHeroUICore.Parsers;

#elif TTWARS

using TTWarsCore.Parsers;

#endif

namespace MainCore.Tasks.Update
{
    public class UpdateVillage : UpdateInfo
    {
        public UpdateVillage(int villageId, int accountId) : base(accountId)
        {
            VillageId = villageId;
        }

        public int VillageId { get; protected set; }

        public override async Task Execute()
        {
            if (!IsCorrectVillage())
            {
                await SwitchVillage();
            }

            await base.Execute();
            await Update();
        }

        private async Task Update()
        {
            var currentUrl = ChromeBrowser.GetCurrentUrl();
            var tasks = new List<Task>();

            if (currentUrl.Contains("dorf1"))
            {
                tasks.Add(UpdateDorf1());
                tasks.Add(UpdateCurrentlyBuilding());
            }
            else if (currentUrl.Contains("dorf2"))
            {
                tasks.Add(UpdateDorf2());
                tasks.Add(UpdateCurrentlyBuilding());
            }

            tasks.Add(UpdateResource());

            await Task.WhenAll(tasks);
        }

        private bool IsCorrectVillage()
        {
            var html = ChromeBrowser.GetHtml();

            var listNode = VillagesTable.GetVillageNodes(html);
            foreach (var node in listNode)
            {
                var id = VillagesTable.GetId(node);
                if (id != VillageId) continue;
                return VillagesTable.IsActive(node);
            }
            return false;
        }

        private async Task SwitchVillage()
        {
            await Task.Delay(1000);
        }

        private async Task UpdateResource()
        {
            var html = ChromeBrowser.GetHtml();
            using var context = ContextFactory.CreateDbContext();
            var resource = context.VillagesResources.Find(VillageId);

            if (resource is null)
            {
                context.VillagesResources.Add(new()
                {
                    VillageId = VillageId,
                    Wood = StockBar.GetWood(html),
                    Clay = StockBar.GetClay(html),
                    Iron = StockBar.GetIron(html),
                    Crop = StockBar.GetCrop(html),
                    Warehouse = StockBar.GetWarehouseCapacity(html),
                    Granary = StockBar.GetGranaryCapacity(html),
                    FreeCrop = StockBar.GetFreeCrop(html),
                });
            }
            else
            {
                resource.Wood = StockBar.GetWood(html);
                resource.Clay = StockBar.GetClay(html);
                resource.Iron = StockBar.GetIron(html);
                resource.Crop = StockBar.GetCrop(html);
                resource.Warehouse = StockBar.GetWarehouseCapacity(html);
                resource.Granary = StockBar.GetGranaryCapacity(html);
                resource.FreeCrop = StockBar.GetFreeCrop(html);
            }

            var updateTime = context.VillagesUpdateTime.Find(VillageId);
            if (updateTime is null)
            {
                context.VillagesUpdateTime.Add(new()
                {
                    VillageId = VillageId,
                    Resource = DateTime.Now,
                });
            }
            else
            {
                updateTime.Resource = DateTime.Now;
            }

            await context.SaveChangesAsync();
        }

        private async Task UpdateCurrentlyBuilding()
        {
            await Task.Delay(1000);
        }

        private async Task UpdateDorf1()
        {
            var html = ChromeBrowser.GetHtml();
            var resFields = VillageFields.GetResourceNodes(html);
            using var context = ContextFactory.CreateDbContext();
            foreach (var fieldNode in resFields)
            {
                var id = VillageFields.GetId(fieldNode);
                var resource = context.VillagesBuildings.Find(new { VillageId, id });
                if (resource is null)
                {
                    context.VillagesBuildings.Add(new()
                    {
                        VillageId = VillageId,
                        Id = id,
                        Level = VillageFields.GetLevel(fieldNode),
                        Type = VillageFields.GetType(fieldNode),
                        IsUnderConstruction = VillageFields.IsUnderConstruction(fieldNode),
                    });
                }
                else
                {
                    resource.Level = VillageFields.GetLevel(fieldNode);
                    resource.Type = VillageFields.GetType(fieldNode);
                    resource.IsUnderConstruction = VillageFields.IsUnderConstruction(fieldNode);
                }
            }
            var updateTime = context.VillagesUpdateTime.Find(VillageId);
            if (updateTime is null)
            {
                context.VillagesUpdateTime.Add(new()
                {
                    VillageId = VillageId,
                    Dorf1 = DateTime.Now,
                });
            }
            else
            {
                updateTime.Dorf1 = DateTime.Now;
            }
            await context.SaveChangesAsync();
        }

        private async Task UpdateDorf2()
        {
            var html = ChromeBrowser.GetHtml();
            var buildingNodes = VillageInfrastructure.GetBuildingNodes(html);
            using var context = ContextFactory.CreateDbContext();
            foreach (var buildingNode in buildingNodes)
            {
                var id = VillageFields.GetId(buildingNode);
                var building = context.VillagesBuildings.Find(new { VillageId, id });
                if (building is null)
                {
                    context.VillagesBuildings.Add(new()
                    {
                        VillageId = VillageId,
                        Id = id,
                        Level = VillageFields.GetLevel(buildingNode),
                        Type = VillageFields.GetType(buildingNode),
                        IsUnderConstruction = VillageFields.IsUnderConstruction(buildingNode),
                    });
                }
                else
                {
                    building.Level = VillageFields.GetLevel(buildingNode);
                    building.Type = VillageFields.GetType(buildingNode);
                    building.IsUnderConstruction = VillageFields.IsUnderConstruction(buildingNode);
                }
            }
            var updateTime = context.VillagesUpdateTime.Find(VillageId);
            if (updateTime is null)
            {
                context.VillagesUpdateTime.Add(new()
                {
                    VillageId = VillageId,
                    Dorf2 = DateTime.Now,
                });
            }
            else
            {
                updateTime.Dorf2 = DateTime.Now;
            }
            await context.SaveChangesAsync();
        }

        public override string Name => $"Update village {VillageId}";
    }
}