﻿using FluentResults;
using HtmlAgilityPack;
using MainCore.Commands.Base;
using MainCore.Commands.General;
using MainCore.Common.Errors;
using MainCore.Infrasturecture.AutoRegisterDi;
using MainCore.Parsers;
using MainCore.Services;
using MediatR;
using OpenQA.Selenium;

namespace MainCore.Commands.Features.Step.ClaimQuest.CollectRewardCommandHandler
{
    [RegisterAsTransient(Common.Enums.ServerEnums.TravianOfficial)]
    public class TravianOfficial : ICommandHandler<CollectRewardCommand>
    {
        private readonly IChromeManager _chromeManager;
        private readonly UnitOfParser _unitOfParser;
        private readonly IMediator _mediator;

        public TravianOfficial(IChromeManager chromeManager, UnitOfParser unitOfParser, IMediator mediator)
        {
            _chromeManager = chromeManager;
            _unitOfParser = unitOfParser;
            _mediator = mediator;
        }

        public async Task<Result> Handle(CollectRewardCommand command, CancellationToken cancellationToken)
        {
            var chromeBrowser = _chromeManager.Get(command.AccountId);

            HtmlDocument html;
            Result result;
            do
            {
                if (cancellationToken.IsCancellationRequested) return Cancel.Error;
                html = chromeBrowser.Html;
                var quest = _unitOfParser.QuestParser.GetQuestCollectButton(html);

                if (quest is null)
                {
                    result = await Handle(chromeBrowser);
                    if (result.IsFailed) return result.WithError(TraceMessage.Error(TraceMessage.Line()));

                    await _mediator.Send(new DelayClickCommand(command.AccountId), cancellationToken);
                    continue;
                }

                result = await chromeBrowser.Click(By.XPath(quest.XPath));
                if (result.IsFailed) return result.WithError(TraceMessage.Error(TraceMessage.Line()));

                await _mediator.Send(new DelayClickCommand(command.AccountId), cancellationToken);
            }
            while (_unitOfParser.QuestParser.IsQuestClaimable(html));
            return Result.Ok();
        }

        private async Task<Result> Handle(IChromeBrowser chromeBrowser)
        {
            var html = chromeBrowser.Html;
            var firstTab = _unitOfParser.NavigationTabParser.GetTab(html, 0);
            if (firstTab is null) return Retry.NotFound("tasks", "tab");

            var firstTabActive = _unitOfParser.NavigationTabParser.IsTabActive(firstTab);

            Result result;
            if (firstTabActive)
            {
                var secondTab = _unitOfParser.NavigationTabParser.GetTab(html, 1);
                result = await chromeBrowser.Click(By.XPath(secondTab.XPath));
                if (result.IsFailed) return result.WithError(TraceMessage.Error(TraceMessage.Line()));
            }
            else
            {
                result = await chromeBrowser.Click(By.XPath(firstTab.XPath));
                if (result.IsFailed) return result.WithError(TraceMessage.Error(TraceMessage.Line()));
            }

            return Result.Ok();
        }
    }
}