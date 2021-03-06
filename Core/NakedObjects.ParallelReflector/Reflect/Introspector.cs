// Copyright Naked Objects Group Ltd, 45 Station Road, Henley on Thames, UK, RG9 1AT
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0.
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Common.Logging;
using NakedObjects.Architecture.Adapter;
using NakedObjects.Architecture.Component;
using NakedObjects.Architecture.FacetFactory;
using NakedObjects.Architecture.Reflect;
using NakedObjects.Architecture.SpecImmutable;
using NakedObjects.Core;
using NakedObjects.Core.Util;
using NakedObjects.Meta.Adapter;
using NakedObjects.Meta.SpecImmutable;
using NakedObjects.Util;

namespace NakedObjects.ParallelReflect {
    public sealed class Introspector : IIntrospector {
        private static readonly ILog Log = LogManager.GetLogger(typeof (Introspector));
        private readonly IReflector reflector;
        private MethodInfo[] methods;
        private List<IAssociationSpecImmutable> orderedFields;
        private List<IActionSpecImmutable> orderedObjectActions;
        private PropertyInfo[] properties;

        public Introspector(IReflector reflector) {
            this.reflector = reflector;
        }

        private IClassStrategy ClassStrategy => reflector.ClassStrategy;

        private IFacetFactorySet FacetFactorySet => reflector.FacetFactorySet;

        private Type[] InterfacesTypes {
            get { return IntrospectedType.GetInterfaces().Where(i => i.IsPublic).ToArray(); }
        }

        private Type SuperclassType => IntrospectedType.BaseType;

        #region IIntrospector Members

        public Type IntrospectedType { get; private set; }

        /// <summary>
        ///     As per <see cref="MemberInfo.Name" />
        /// </summary>
        public string ClassName => IntrospectedType.Name;

        public string FullName => IntrospectedType.GetProxiedTypeFullName();

        public string ShortName => TypeNameUtils.GetShortName(IntrospectedType.Name);

        public IList<IAssociationSpecImmutable> Fields => orderedFields.ToImmutableList();

        public IList<IActionSpecImmutable> ObjectActions => orderedObjectActions.ToImmutableList();

        public ITypeSpecBuilder[] Interfaces { get; set; }
        public ITypeSpecBuilder Superclass { get; set; }

        public void IntrospectType(Type typeToIntrospect, ITypeSpecImmutable specification) {
            throw new NotImplementedException();
        }

        public ImmutableDictionary<string, ITypeSpecBuilder> IntrospectType(Type typeToIntrospect, ITypeSpecImmutable spec, ImmutableDictionary<string, ITypeSpecBuilder> metamodel) {

            if (!TypeUtils.IsPublic(typeToIntrospect)) {
                throw new ReflectionException(Log.LogAndReturn(string.Format(Resources.NakedObjects.DomainClassReflectionError, typeToIntrospect)));
            }

            var fullName = typeToIntrospect.FullName;

            IntrospectedType = typeToIntrospect;
            properties = typeToIntrospect.GetProperties();
            methods = GetNonPropertyMethods();

            // Process facets at object level
            // this will also remove some methods, such as the superclass methods.
            var methodRemover = new IntrospectorMethodRemover(methods);
            metamodel = FacetFactorySet.Process(reflector, IntrospectedType, methodRemover, spec, metamodel);

            if (SuperclassType != null && ClassStrategy.IsTypeToBeIntrospected(SuperclassType)) {
                var result = reflector.LoadSpecification(SuperclassType, metamodel);
                metamodel = result.Item2;
                Superclass = result.Item1;
            }

            AddAsSubclass(spec);

            var interfaces = new List<ITypeSpecBuilder>();
            foreach (Type interfaceType in InterfacesTypes) {
                if (interfaceType != null && ClassStrategy.IsTypeToBeIntrospected(interfaceType)) {
                    var result = reflector.LoadSpecification(interfaceType, metamodel);
                    metamodel = result.Item2;
                    var interfaceSpec = result.Item1;
                    interfaceSpec.AddSubclass(spec);
                    interfaces.Add(interfaceSpec);
                }
            }
            Interfaces = interfaces.ToArray();
            metamodel = IntrospectPropertiesAndCollections(spec, metamodel);
            metamodel = IntrospectActions(spec, metamodel);

            return metamodel;
        }

        #endregion

        private void AddAsSubclass(ITypeSpecImmutable spec) {
            Superclass?.AddSubclass(spec);
        }

        public ImmutableDictionary<string, ITypeSpecBuilder> IntrospectPropertiesAndCollections(ITypeSpecImmutable spec, ImmutableDictionary<string, ITypeSpecBuilder> metamodel) {
            var objectSpec = spec as IObjectSpecImmutable;
            if (objectSpec != null) {
                var result = FindAndCreateFieldSpecs(objectSpec, metamodel);
                metamodel = result.Item2;
                orderedFields = CreateSortedListOfMembers(result.Item1);
            }
            else {
                orderedFields = new List<IAssociationSpecImmutable>();
            }

            return metamodel;
        }

        public ImmutableDictionary<string, ITypeSpecBuilder> IntrospectActions(ITypeSpecImmutable spec, ImmutableDictionary<string, ITypeSpecBuilder> metamodel) {
            // find the actions ...
            var result = FindActionMethods(spec, metamodel);
            IActionSpecImmutable[] findObjectActionMethods = result.Item1;
            metamodel = result.Item2;
            orderedObjectActions = CreateSortedListOfMembers(findObjectActionMethods);
            return metamodel;
        }

        private MethodInfo[] GetNonPropertyMethods() {
            // no better way to do this (ie no flag that indicates getter/setter)
            var allMethods = new List<MethodInfo>(IntrospectedType.GetMethods());
            foreach (PropertyInfo pInfo in properties) {
                allMethods.Remove(pInfo.GetGetMethod());
                allMethods.Remove(pInfo.GetSetMethod());
            }
            allMethods.Sort(new SortActionsFirst(FacetFactorySet));
            return allMethods.ToArray();
        }

        private Tuple<IAssociationSpecImmutable[], ImmutableDictionary<string, ITypeSpecBuilder>> FindAndCreateFieldSpecs(IObjectSpecImmutable spec, ImmutableDictionary<string, ITypeSpecBuilder> metamodel) {

            // now create fieldSpecs for value properties, for collections and for reference properties
            IList<PropertyInfo> collectionProperties = FacetFactorySet.FindCollectionProperties(properties, ClassStrategy).Where(pi => !FacetFactorySet.Filters(pi, ClassStrategy)).ToList();
            var result1 = CreateCollectionSpecs(collectionProperties, spec, metamodel);

            IEnumerable<IAssociationSpecImmutable> collectionSpecs = result1.Item1;
            metamodel = result1.Item2;

            // every other accessor is assumed to be a reference property.
            IList<PropertyInfo> allProperties = FacetFactorySet.FindProperties(properties, ClassStrategy).Where(pi => !FacetFactorySet.Filters(pi, ClassStrategy)).ToList();
            IEnumerable<PropertyInfo> refProperties = allProperties.Except(collectionProperties);

            var result = CreateRefPropertySpecs(refProperties, spec, metamodel);
            var refSpecs = result.Item1;
            metamodel = result.Item2;

            return new Tuple<IAssociationSpecImmutable[], ImmutableDictionary<string, ITypeSpecBuilder>>(collectionSpecs.Union(refSpecs).ToArray(), metamodel);
        }

        private Tuple<IEnumerable<IAssociationSpecImmutable>, ImmutableDictionary<string, ITypeSpecBuilder>> CreateCollectionSpecs(IEnumerable<PropertyInfo> collectionProperties, IObjectSpecImmutable spec, ImmutableDictionary<string, ITypeSpecBuilder> metamodel) {
            var specs = new List<IAssociationSpecImmutable>();

            foreach (PropertyInfo property in collectionProperties) {

                IIdentifier identifier = new IdentifierImpl(FullName, property.Name);

                // create a collection property spec
                Type returnType = property.PropertyType;
                Type defaultType = typeof(object);
                var result = reflector.LoadSpecification(returnType, metamodel);

                metamodel = result.Item2;
                var returnSpec = result.Item1 as IObjectSpecImmutable;
                result = reflector.LoadSpecification(defaultType, metamodel);
                metamodel = result.Item2;
                var defaultSpec = result.Item1 as IObjectSpecImmutable;

                var collection = ImmutableSpecFactory.CreateOneToManyAssociationSpecImmutable(identifier, spec, returnSpec, defaultSpec);

                metamodel = FacetFactorySet.Process(reflector, property, new IntrospectorMethodRemover(methods), collection, FeatureType.Collections, metamodel);
                specs.Add(collection);
            }
            return new Tuple<IEnumerable<IAssociationSpecImmutable>, ImmutableDictionary<string, ITypeSpecBuilder>>(specs, metamodel);
        }

        /// <summary>
        ///     Creates a list of Association fields for all the properties that use NakedObjects.
        /// </summary>
      

        private Tuple<IEnumerable<IAssociationSpecImmutable>, ImmutableDictionary<string, ITypeSpecBuilder>> CreateRefPropertySpecs(IEnumerable<PropertyInfo> foundProperties, IObjectSpecImmutable spec, ImmutableDictionary<string, ITypeSpecBuilder> metamodel) {
            var specs = new List<IAssociationSpecImmutable>();

            foreach (PropertyInfo property in foundProperties) {

                // create a reference property spec
                var identifier = new IdentifierImpl(FullName, property.Name);
                Type propertyType = property.PropertyType;
                var result = reflector.LoadSpecification(propertyType, metamodel);
                metamodel = result.Item2;
                var propertySpec = result.Item1;
                if (propertySpec is IServiceSpecImmutable) {
                    throw new ReflectionException(Log.LogAndReturn($"Type {propertyType.Name} is a service and cannot be used in public property {property.Name} on type {property.DeclaringType?.Name}. If the property is intended to be an injected service it should have a protected get."));
                }
                var referenceProperty = ImmutableSpecFactory.CreateOneToOneAssociationSpecImmutable(identifier, spec, propertySpec as IObjectSpecImmutable);

                // Process facets for the property
                metamodel = FacetFactorySet.Process(reflector, property, new IntrospectorMethodRemover(methods), referenceProperty, FeatureType.Properties, metamodel);
                specs.Add(referenceProperty);
            }

            return new Tuple<IEnumerable<IAssociationSpecImmutable>, ImmutableDictionary<string, ITypeSpecBuilder>>(specs, metamodel);
        }

       



        private Tuple<IActionSpecImmutable[], ImmutableDictionary<string, ITypeSpecBuilder>> FindActionMethods(ITypeSpecImmutable spec, ImmutableDictionary<string, ITypeSpecBuilder> metamodel) {
            var actionSpecs = new List<IActionSpecImmutable>();
            var actions = FacetFactorySet.FindActions(methods.Where(m => m != null).ToArray(), reflector.ClassStrategy).Where(a => !FacetFactorySet.Filters(a, reflector.ClassStrategy)).ToArray();
            methods = methods.Except(actions).ToArray();

            // ReSharper disable once ForCanBeConvertedToForeach
            // kepp for look as actions are nulled out within loop
            for (int i = 0; i < actions.Length; i++) {
                MethodInfo actionMethod = actions[i];

                // actions are potentially being nulled within this loop
                if (actionMethod != null) {
                    string fullMethodName = actionMethod.Name;

                    Type[] parameterTypes = actionMethod.GetParameters().Select(parameterInfo => parameterInfo.ParameterType).ToArray();

                    // build action & its parameters

                    if (actionMethod.ReturnType != typeof(void)) {
                        metamodel = reflector.LoadSpecification(actionMethod.ReturnType, metamodel).Item2;
                    }

                    IIdentifier identifier = new IdentifierImpl(FullName, fullMethodName, actionMethod.GetParameters().ToArray());
                    //IActionParameterSpecImmutable[] actionParams = parameterTypes.
                    //    Select(pt => ImmutableSpecFactory.CreateActionParameterSpecImmutable(reflector.LoadSpecification<IObjectSpecImmutable>(pt, metamodel), identifier)).ToArray();

                    var actionParams = new List<IActionParameterSpecImmutable>();

                    foreach (var pt in parameterTypes) {
                        var result = reflector.LoadSpecification(pt, metamodel);
                        metamodel = result.Item2;
                        var ospec = result.Item1 as IObjectSpecImmutable;
                        var actionSpec = ImmutableSpecFactory.CreateActionParameterSpecImmutable(ospec, identifier);
                        actionParams.Add(actionSpec);
                    }

                    var action = ImmutableSpecFactory.CreateActionSpecImmutable(identifier, spec, actionParams.ToArray());

                    // Process facets on the action & parameters
                    metamodel = FacetFactorySet.Process(reflector, actionMethod, new IntrospectorMethodRemover(actions), action, FeatureType.Actions, metamodel);
                    for (int l = 0; l < actionParams.Count; l++) {
                        metamodel = FacetFactorySet.ProcessParams(reflector, actionMethod, l, actionParams[l], metamodel);
                    }

                    actionSpecs.Add(action);
                }
            }

            return new Tuple<IActionSpecImmutable[], ImmutableDictionary<string, ITypeSpecBuilder>>(actionSpecs.ToArray(), metamodel);
        }


        private static List<T> CreateSortedListOfMembers<T>(T[] members) where T : IMemberSpecImmutable {
            var list = new List<T>(members);
            list.Sort(new MemberOrderComparator<T>());
            return list;
        }

        #region Nested type: IntrospectorMethodRemover

        #region Nested Type: DotnetIntrospectorMethodRemover

        private class IntrospectorMethodRemover : IMethodRemover {
            private readonly MethodInfo[] methods;

            public IntrospectorMethodRemover(MethodInfo[] methods) {
                this.methods = methods;
            }

            #region IMethodRemover Members

            public void RemoveMethod(MethodInfo methodToRemove) {
                for (int i = 0; i < methods.Length; i++) {
                    if (methods[i] != null) {
                        if (methods[i].MemberInfoEquals(methodToRemove)) {
                            methods[i] = null;
                        }
                    }
                }
            }

            public void RemoveMethods(IList<MethodInfo> methodList) {
                for (int i = 0; i < methods.Length; i++) {
                    if (methods[i] != null) {
                        if (methodList.Any(methodToRemove => methods[i].MemberInfoEquals(methodToRemove))) {
                            methods[i] = null;
                        }
                    }
                }
            }

            #endregion
        }

        #endregion

        #endregion

        #region Nested type: SortActionsFirst

        private class SortActionsFirst : IComparer<MethodInfo> {
            private readonly IFacetFactorySet factories;

            public SortActionsFirst(IFacetFactorySet factories) {
                this.factories = factories;
            }

            #region IComparer<MethodInfo> Members

            public int Compare(MethodInfo x, MethodInfo y) {
                bool xIsRecognised = x != null && factories.Recognizes(x);
                bool yIsRecognised = y != null && factories.Recognizes(y);

                if (xIsRecognised == yIsRecognised) {
                    return 0;
                }

                return xIsRecognised ? 1 : -1;
            }

            #endregion
        }

        #endregion
    }

    // Copyright (c) Naked Objects Group Ltd.
}