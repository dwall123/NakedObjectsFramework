// Copyright Naked Objects Group Ltd, 45 Station Road, Henley on Thames, UK, RG9 1AT
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0.
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using NakedObjects.Architecture.Component;
using NakedObjects.Architecture.Facet;
using NakedObjects.Architecture.FacetFactory;
using NakedObjects.Architecture.Reflect;
using NakedObjects.Architecture.Spec;
using NakedObjects.Meta.Facet;
using NakedObjects.Meta.Utils;

namespace NakedObjects.Reflect.FacetFactory {
    public sealed class DateOnlyFacetFactory : AnnotationBasedFacetFactoryAbstract {
        public DateOnlyFacetFactory(int numericOrder)
            : base(numericOrder, FeatureType.PropertiesAndActionParameters) {}

        private static void Process(MemberInfo member, ISpecification holder) {
            var dataTypeAttribute = member.GetCustomAttribute<DataTypeAttribute>();
            var concurrencyCheckAttribute = member.GetCustomAttribute<ConcurrencyCheckAttribute>();
            FacetUtils.AddFacet(Create(dataTypeAttribute, concurrencyCheckAttribute, holder));
        }

        private static bool IsDatetimeOrNullableDateTime(Type type) {
            return type == typeof (DateTime) || type == typeof (DateTime?);
        }

        public override void Process(IReflector reflector, PropertyInfo property, IMethodRemover methodRemover, ISpecificationBuilder specification) {
            if (IsDatetimeOrNullableDateTime(property.PropertyType)) {
                Process(property, specification);
            }
        }

        public override void ProcessParams(IReflector reflector, MethodInfo method, int paramNum, ISpecificationBuilder holder) {
            ParameterInfo parameter = method.GetParameters()[paramNum];

            if (IsDatetimeOrNullableDateTime(parameter.ParameterType)) {
                var dataTypeAttribute = parameter.GetCustomAttribute<DataTypeAttribute>();
                var concurrencyCheckAttribute = parameter.GetCustomAttribute<ConcurrencyCheckAttribute>();
                FacetUtils.AddFacet(Create(dataTypeAttribute, concurrencyCheckAttribute, holder));
            }
        }

        private static IDateOnlyFacet Create(DataTypeAttribute attribute, ConcurrencyCheckAttribute concurrencyCheckAttribute, ISpecification holder) {

            if (attribute?.DataType == DataType.Date) {
                return new DateOnlyFacet(holder);
            }

            if (concurrencyCheckAttribute != null) {
                return null;
            }

            return attribute?.DataType == DataType.DateTime ? null : new DateOnlyFacet(holder);
        }
    }
}