// Copyright Naked Objects Group Ltd, 45 Station Road, Henley on Thames, UK, RG9 1AT
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0.
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

using System.Collections.Generic;
using System.Net.Http;
using NakedObjects.Facade;
using NakedObjects.Facade.Contexts;
using RestfulObjects.Snapshot.Representations;
using RestfulObjects.Snapshot.Utility;

namespace RestfulObjects.Snapshot.Strategies {
    public class ActionMemberRepresentationStrategy : AbstractActionRepresentationStrategy {
        public ActionMemberRepresentationStrategy(IOidStrategy oidStrategy, HttpRequestMessage req, ActionContextFacade actionContext, RestControlFlags flags)
            : base(oidStrategy, req, actionContext, flags) {}

        public override LinkRepresentation[] GetLinks() {
            return new List<LinkRepresentation> {CreateDetailsLink(), CreateActionLink()}.ToArray();
        }

        public override bool ShowParameters() {
            return false;
        }
    }
}