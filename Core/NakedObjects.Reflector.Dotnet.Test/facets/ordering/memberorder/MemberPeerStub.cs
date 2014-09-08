// Copyright � Naked Objects Group Ltd ( http://www.nakedobjects.net). 
// All Rights Reserved. This code released under the terms of the 
// Microsoft Public License (MS-PL) ( http://opensource.org/licenses/ms-pl.html) 

using NakedObjects.Architecture.Adapter;
using NakedObjects.Architecture.Facets;
using NakedObjects.Architecture.Persist;
using NakedObjects.Architecture.Reflect;
using NakedObjects.Architecture.Security;
using NakedObjects.Reflector.Peer;

namespace NakedObjects.Reflector.DotNet.Facets.Ordering.MemberOrder {
    internal class MemberPeerStub : NamedAndDescribedFacetHolderImpl, INakedObjectMemberPeer {
        private readonly INakedObjectPersistor persistor;

        public MemberPeerStub(string name, INakedObjectPersistor persistor)
            : base(name) {
            this.persistor = persistor;
        }

        #region INakedObjectMemberPeer Members

        public override IIdentifier Identifier {
            get { return new IdentifierNull(this, persistor); }
        }

        #endregion

        public string GetHelp() {
            return null;
        }

        public IConsent IsUsableDeclaratively() {
            return Allow.Default;
        }

        public IConsent IsUsableForSession(ISession session) {
            return Allow.Default;
        }

        public IConsent IsUsable(INakedObject target) {
            return null;
        }

        public bool IsVisibleDeclaratively() {
            return false;
        }

        public bool IsVisibleForSession(ISession session) {
            return false;
        }

        public bool IsVisible(INakedObject target) {
            return false;
        }

        public override string ToString() {
            return GetName(persistor);
        }

        #region Nested Type: IdentifierNull

        private class IdentifierNull : IdentifierImpl {
            private readonly MemberPeerStub owner;
            private readonly INakedObjectPersistor persistor;

            public IdentifierNull(MemberPeerStub owner, INakedObjectPersistor persistor)
                : base(null, "", "") {
                this.owner = owner;
                this.persistor = persistor;
            }

            public override string ToString() {
                return owner.GetName(persistor);
            }
        };

        #endregion
    }
}