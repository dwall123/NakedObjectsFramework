// Copyright � Naked Objects Group Ltd ( http://www.nakedobjects.net). 
// All Rights Reserved. This code released under the terms of the 
// Microsoft Public License (MS-PL) ( http://opensource.org/licenses/ms-pl.html) 

using System;
using NakedObjects.Architecture.Adapter;
using NakedObjects.Architecture.Component;
using NakedObjects.Architecture.Facet;
using NakedObjects.Architecture.Facets.Properties.Modify;
using NakedObjects.Architecture.Persist;
using NakedObjects.Architecture.Resolve;

namespace NakedObjects.Reflector.Transaction.Facets.Properties.Write {
    public class PropertyClearFacetWrapTransaction : PropertyClearFacetAbstract {
        private readonly IPropertyClearFacet underlyingFacet;

        public PropertyClearFacetWrapTransaction(IPropertyClearFacet underlyingFacet)
            : base(underlyingFacet.Specification) {
            this.underlyingFacet = underlyingFacet;
        }

        public override void ClearProperty(INakedObject inObject, ITransactionManager transactionManager) {
          
            if (inObject.ResolveState.IsPersistent()) {
                try {
                    transactionManager.StartTransaction();
                    underlyingFacet.ClearProperty(inObject, transactionManager);
                    transactionManager.EndTransaction();
                }
                catch (Exception) {
                    throw;
                }
            }
            else {
                underlyingFacet.ClearProperty(inObject, transactionManager);
            }
        }

        public override string ToString() {
            return base.ToString() + " --> " + underlyingFacet;
        }
    }


    // Copyright (c) Naked Objects Group Ltd.
}