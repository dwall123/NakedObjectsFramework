// Copyright � Naked Objects Group Ltd ( http://www.nakedobjects.net). 
// All Rights Reserved. This code released under the terms of the 
// Microsoft Public License (MS-PL) ( http://opensource.org/licenses/ms-pl.html) 

using Common.Logging;
using NakedObjects.Architecture.Component;
using NakedObjects.Architecture.Persist;
using NakedObjects.Architecture.Transaction;
using NakedObjects.Core.Persist;

namespace NakedObjects.Persistor.Objectstore {
    public class ObjectStoreTransactionManager : ITransactionManager {
        private static readonly ILog Log;
        private readonly IObjectStore objectStore;
        private ITransaction transaction;
        private int transactionLevel;
        private bool userAborted;

        static ObjectStoreTransactionManager() {
            Log = LogManager.GetLogger(typeof (ObjectStoreTransactionManager));
        }

        public ObjectStoreTransactionManager(IObjectStore objectStore) {
            this.objectStore = objectStore;
        }

        public virtual ITransaction Transaction {
            get {
                if (transaction == null) {
                    return new ObjectStoreTransaction(objectStore);
                }
                return transaction;
            }
        }

        #region INakedObjectTransactionManager Members

        public virtual void StartTransaction() {
            if (transaction == null) {
                transaction = new ObjectStoreTransaction(objectStore);
                transactionLevel = 0;
                userAborted = false;
                objectStore.StartTransaction();
            }
            transactionLevel++;
        }

        public virtual bool FlushTransaction() {
            if (transaction != null) {
                return transaction.Flush();
            }
            return false;
        }

        public virtual void AbortTransaction() {
            if (transaction != null) {
                transaction.Abort();
                transaction = null;
                transactionLevel = 0;
                objectStore.AbortTransaction();
            }
        }

        public virtual void UserAbortTransaction() {
            AbortTransaction();
            userAborted = true;
        }


        public virtual void EndTransaction() {
            transactionLevel--;
            if (transactionLevel == 0) {
                Transaction.Commit();
                transaction = null;
            }
            else if (transactionLevel < 0) {
                transactionLevel = 0;
                if (!userAborted) {
                    throw new TransactionException("No transaction running to end");
                }
            }
        }

        public virtual void AddCommand(IPersistenceCommand command) {
            Transaction.AddCommand(command);
        }

        #endregion
    }


    // Copyright (c) Naked Objects Group Ltd.
}