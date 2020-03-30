using System;
using System.Collections.Generic;
using System.Linq;

namespace Domain.Bank
{
    public class Account
    {
        public Guid Id { get; set; }
        public string Owner { get; set; }

        public Account()
        {
            Id = new Guid();
        }
        public List<decimal> Credits;
        public List<decimal> Debits;
        public decimal Balance
        {
            get
            {
                return Credits.Sum() - Debits.Sum();
            }
        }
        public void Deposit(decimal amount)
        {
            Credits.Add(amount);
        }
        public void Withdraw(decimal ammount)
        {
            Debits.Add(ammount);
        }
    }
}
