﻿using FluentValidation;
using MainCore.UI.Models.Input;

namespace MainCore.Commands.UI.AccountSetting
{
    public class SaveCommand : ByAccountIdBase, IRequest
    {
        public AccountSettingInput AccountSettingInput { get; }

        public SaveCommand(AccountId accountId, AccountSettingInput accountSettingInput) : base(accountId)
        {
            AccountSettingInput = accountSettingInput;
        }
    }

    public class SaveCommandHandler : IRequestHandler<SaveCommand>
    {
        private readonly IValidator<AccountSettingInput> _accountsettingInputValidator;
        private readonly UnitOfRepository _unitOfRepository;
        private readonly IDialogService _dialogService;
        private readonly IMediator _mediator;

        public SaveCommandHandler(IValidator<AccountSettingInput> accountsettingInputValidator, UnitOfRepository unitOfRepository, IDialogService dialogService, IMediator mediator)
        {
            _accountsettingInputValidator = accountsettingInputValidator;
            _unitOfRepository = unitOfRepository;
            _dialogService = dialogService;
            _mediator = mediator;
        }

        public async Task Handle(SaveCommand request, CancellationToken cancellationToken)
        {
            var accountId = request.AccountId;
            var accountSettingInput = request.AccountSettingInput;
            var result = _accountsettingInputValidator.Validate(accountSettingInput);
            if (!result.IsValid)
            {
                _dialogService.ShowMessageBox("Error", result.ToString());
                return;
            }

            var settings = accountSettingInput.Get();
            _unitOfRepository.AccountSettingRepository.Update(accountId, settings);
            await _mediator.Publish(new AccountSettingUpdated(accountId), cancellationToken);
            _dialogService.ShowMessageBox("Information", message: "Settings saved");
        }
    }
}