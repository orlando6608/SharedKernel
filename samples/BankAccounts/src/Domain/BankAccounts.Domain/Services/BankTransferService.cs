﻿using BankAccounts.Domain.BankAccounts;
using BankAccounts.Domain.BankAccounts.Exceptions;
using System;

namespace BankAccounts.Domain.Services
{
    public class BankTransferService
    {
        public void Transfer(BankAccount fromBankAccount, BankAccount toBankAccount, decimal quantity, DateTime date,
            Guid fromMovementId, Guid toMovementId)
        {
            if (quantity <= 0)
                throw new QuantityCannotBeNegativeException();

            fromBankAccount.WithdrawMoney(fromMovementId, "Transfer", quantity, date);

            toBankAccount.MakeDeposit(toMovementId, "Transfer", quantity, date);
        }
    }
}
