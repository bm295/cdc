using Domain.Bank;
using System;
using System.Collections.Generic;
using Xunit;

namespace UnitTest
{
    public class Bank
    {
        [Fact]
        public void AccountId_Should_Be_Guid()
        {
            var account = new Account();
            Assert.IsType<Guid>(account.Id);
        }

        [Fact]        
        public void AccountBalance_Computation()
        {
            var account = new Account
            {
                Credits = new List<decimal> { 1500 },
                Debits = new List<decimal> { 500 }
            };
            Assert.Equal(1000, account.Balance);
        }
    }
}
