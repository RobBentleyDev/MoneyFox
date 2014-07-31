﻿using MoneyManager.DataAccess;
using MoneyTracker.Models;
using MoneyTracker.Src;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Media.Transcoding;

namespace MoneyTracker.ViewModels
{
    [ImplementPropertyChanged]
    public class TransactionDAO : AbstractDataAccess<FinancialTransaction>
    {
        public ObservableCollection<FinancialTransaction> AllTransactions { get; set; }

        public ObservableCollection<FinancialTransaction> RelatedTransactions { get; set; }

        public FinancialTransaction SelectedTransaction { get; set; }

        protected override void SaveToDb(FinancialTransaction transaction)
        {
            using (var dbConn = ConnectionFactory.GetDbConnection())
            {
                if (AllTransactions == null)
                {
                    AllTransactions = new ObservableCollection<FinancialTransaction>();
                }

                App.AccountViewModel.AddTransactionAmount(transaction);

                AllTransactions.Add(transaction);
                dbConn.Insert(transaction, typeof(FinancialTransaction));
            }
        }

        protected override void DeleteFromDatabase(FinancialTransaction transaction)
        {
            using (var dbConn = ConnectionFactory.GetDbConnection())
            {
                if (RelatedTransactions != null && RelatedTransactions.Contains(transaction))
                {
                    RelatedTransactions.Remove(transaction);
                }

                AllTransactions.Remove(transaction);
                dbConn.Delete(transaction);

                transaction.Amount = -transaction.Amount;

                App.AccountViewModel.AddTransactionAmount(transaction);
            }
        }

        public void DeleteAssociatedTransactionsFromDatabase(int accountId)
        {
            using (var dbConn = ConnectionFactory.GetDbConnection())
            {
                if (AllTransactions == null)
                {
                    AllTransactions = new ObservableCollection<FinancialTransaction>();
                }

                var transactions = dbConn.Table<FinancialTransaction>()
                    .Where(x => x.ChargedAccountId == accountId)
                    .ToList();

                foreach (var transaction in transactions)
                {
                    AllTransactions.Remove(transaction);
                    dbConn.Delete(transaction);
                }
            }
        }

        protected override void GetListFromDb()
        {
            using (var dbConn = ConnectionFactory.GetDbConnection())
            {
                AllTransactions = new ObservableCollection<FinancialTransaction>
                    (dbConn.Table<FinancialTransaction>().ToList());
            }
        }

        public void GetRelatedTransactions()
        {
            var accountId = App.AccountViewModel.SelectedAccount.Id;
            RelatedTransactions = new ObservableCollection<FinancialTransaction>(
                AllTransactions
                    .Where(x => x.ChargedAccountId == accountId).ToList());
        }

        protected override void UpdateItem(FinancialTransaction transaction)
        {
            using (var dbConn = ConnectionFactory.GetDbConnection())
            {
                dbConn.Update(transaction);
            }
        }

        public void ClearTransaction()
        {
            var transactions = GetUnclearedTransactions();
            foreach (var transaction in transactions)
            {
                App.AccountViewModel.AddTransactionAmount(transaction);
            }
        }

        public List<FinancialTransaction> GetUnclearedTransactions()
        {
            using (var dbConn = ConnectionFactory.GetDbConnection())
            {
                return dbConn.Table<FinancialTransaction>().Where(x => x.Cleared == false
                    && x.Date <= DateTime.Now).ToList();
            }
        }
    }
}