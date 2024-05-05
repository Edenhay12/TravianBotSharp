﻿namespace MainCore.Tasks.Base
{
    public abstract class AccountTask : TaskBase
    {
        public AccountId AccountId { get; protected set; }

        private ILoginPageParser _loginPageParser;

        protected IChromeBrowser _chromeBrowser;

        private readonly IChromeManager _chromeManager;

        protected AccountTask(IMediator mediator) : base(mediator)
        {
            _chromeManager = Locator.Current.GetService<IChromeManager>();
        }

        public void Setup(AccountId accountId, CancellationToken cancellationToken = default)
        {
            AccountId = accountId;
            CancellationToken = cancellationToken;
        }

        protected override async Task<Result> PreExecute()
        {
            if (CancellationToken.IsCancellationRequested) return Cancel.Error;
            _chromeBrowser = _chromeManager.Get(AccountId);

            if (IsIngame())
            {
                await new UpdateAccountInfoCommand().Execute(_chromeBrowser, AccountId, CancellationToken);
                await new UpdateVillageListCommand().Execute(_chromeBrowser, AccountId, CancellationToken);
                return Result.Ok();
            }

            _loginPageParser ??= Locator.Current.GetService<ILoginPageParser>();

            if (IsLogin())
            {
                if (this is not LoginTask)
                {
                    ExecuteAt = ExecuteAt.AddMilliseconds(1975);
                    await _mediator.Publish(new AccountLogout(AccountId), CancellationToken);
                    return Skip.AccountLogout;
                }
                return Result.Ok();
            }

            return Stop.NotTravianPage;
        }

        protected override async Task<Result> PostExecute()
        {
            await new UpdateAccountInfoCommand().Execute(_chromeBrowser, AccountId, CancellationToken);
            await new UpdateVillageListCommand().Execute(_chromeBrowser, AccountId, CancellationToken);
            await new CheckAdventureCommand().Execute(_chromeBrowser, AccountId, CancellationToken);
            return Result.Ok();
        }

        private bool IsIngame()
        {
            var html = _chromeBrowser.Html;

            var serverTime = html.GetElementbyId("servertime");

            return serverTime is not null;
        }

        private bool IsLogin()
        {
            var html = _chromeBrowser.Html;

            var loginButton = _loginPageParser.GetLoginButton(html);

            return loginButton is not null;
        }
    }
}