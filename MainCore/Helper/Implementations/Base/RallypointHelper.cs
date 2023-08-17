﻿using FluentResults;
using MainCore.Enums;
using MainCore.Errors;
using MainCore.Helper.Interface;
using MainCore.Parsers.Interface;
using MainCore.Services.Interface;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using System.Linq;

namespace MainCore.Helper.Implementations.Base
{
    public class RallypointHelper : IRallypointHelper
    {
        protected readonly IDbContextFactory<AppDbContext> _contextFactory;
        protected readonly IChromeManager _chromeManager;
        protected readonly IGeneralHelper _generalHelper;
        protected readonly IUpdateHelper _updateHelper;
        protected readonly IFarmListParser _farmListParser;

        public RallypointHelper(IChromeManager chromeManager, IGeneralHelper generalHelper, IDbContextFactory<AppDbContext> contextFactory, IUpdateHelper updateHelper, IFarmListParser farmListParser)
        {
            _chromeManager = chromeManager;
            _generalHelper = generalHelper;
            _contextFactory = contextFactory;
            _updateHelper = updateHelper;
            _farmListParser = farmListParser;
        }

        public Result ClickStartFarm(int accountId, int farmId)
        {
            var chromeBrowser = _chromeManager.Get(accountId);
            var html = chromeBrowser.GetHtml();

            var startButton = _farmListParser.GetStartButton(html, farmId);
            if (startButton is null)
            {
                return Result.Fail(new Retry("Cannot found start button"));
            }

            var result = _generalHelper.Click(accountId, By.XPath(startButton.XPath));
            if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            return Result.Ok();
        }

        public Result EnterFarmListPage(int accountId, int villageId)
        {
            var result = _generalHelper.ToDorf2(accountId, villageId, switchVillage: true);
            if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));

            result = ToRallypoint(accountId, villageId);
            if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));

            result = _generalHelper.SwitchTab(accountId, 4);
            if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));

            _updateHelper.UpdateFarmList(accountId);

            return Result.Ok();
        }

        public Result StartFarmList(int accountId, int villageId)
        {
            var result = EnterFarmListPage(accountId, villageId);
            if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));

            using var context = _contextFactory.CreateDbContext();

            var settings = context.AccountsSettings.Find(accountId);
            if (settings.UseStartAllFarm)
            {
                var chromeBrowser = _chromeManager.Get(accountId);
                var html = chromeBrowser.GetHtml();

                var startAllButton = _farmListParser.GetStartAllButton(html);
                if (startAllButton is null)
                {
                    return Result.Fail(new Retry("Cannot found start all button"));
                }

                result = _generalHelper.Click(accountId, By.XPath(startAllButton.XPath));
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }
            else
            {
                var farms = context.Farms.Where(x => x.AccountId == accountId);
                foreach (var farm in farms)
                {
                    var isActive = context.FarmsSettings.Find(farm.Id).IsActive;
                    if (!isActive) continue;

                    result = ClickStartFarm(accountId, farm.Id);
                    if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
                }
            }
            return Result.Ok();
        }

        public Result ToRallypoint(int accountId, int villageId)
        {
            using var context = _contextFactory.CreateDbContext();
            var rallypoint = context.VillagesBuildings.Where(x => x.VillageId == villageId)
                                                      .FirstOrDefault(x => x.Type == BuildingEnums.RallyPoint && x.Level > 0);
            if (rallypoint is null)
            {
                return Result.Fail(new Skip("Rallypoint is missing"));
            }

            var result = _generalHelper.ToBuilding(accountId, villageId, rallypoint.Id);
            if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));

            return Result.Ok();
        }
    }
}