﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Policy;
using Unity.Resolution;

namespace Unity.Injection
{
    /// <summary>
    /// Base class for objects that can be used to configure what
    /// class members get injected by the container.
    /// </summary>
    public abstract class InjectionMember
    {
        /// <summary>
        /// Add policies to the <paramref name="policies"/> to configure the
        /// container to call this constructor with the appropriate parameter values.
        /// </summary>
        /// <param name="registeredType">Type of interface being registered. If no interface,
        /// this will be null.</param>
        /// <param name="mappedToType">Type of concrete type being registered.</param>
        /// <param name="name">Name used to resolve the type object.</param>
        /// <param name="policies">Policy list to add policies to.</param>
        public virtual void AddPolicies<TContext, TPolicyList>(Type registeredType, Type mappedToType, string name, ref TPolicyList policies)
                where TContext : IResolveContext
                where TPolicyList : IPolicyList
        {
        }

        /// <summary>
        /// This injection member instructs engine, when type mapping is present, 
        /// to build type instead of resolving it
        /// </summary>
        /// <remarks>
        /// When types registered like this:
        /// 
        /// Line 1: container.RegisterType{OtherService}(new ContainerControlledLifetimeManager());  
        /// Line 2: container.RegisterType{IService, OtherService}();
        /// Line 3: container.RegisterType{IOtherService, OtherService}(new InjectionConstructor(container));
        /// 
        /// It is expected that IService resolves instance registered on line 1. But when IOtherService is resolved 
        /// it requires different constructor so it should be built instead.
        /// </remarks>
        public virtual bool BuildRequired => false;
    }


    public abstract class InjectionMember<TMemberInfo, TData> : InjectionMember,
                                                                IEquatable<TMemberInfo>
                                            where TMemberInfo : MemberInfo
    {
        #region Fields

        protected static TData ResolvedValue;

        #endregion


        #region Constructors

        protected InjectionMember(string name, TData data)
        {
            Name = name;
            Data = data;
        }

        protected InjectionMember(TMemberInfo info, TData data)
        {
            MemberInfo = info;
            Name = info.Name;
            Data = data;
        }

        #endregion


        #region Public Members

        public abstract (TMemberInfo, TData) FromType(Type type);

        #endregion


        #region Properties

        protected TData Data { get; set; }

        protected string Name { get; }

        protected TMemberInfo MemberInfo { get; set; }

        #endregion


        #region Methods

        protected abstract IEnumerable<TMemberInfo> DeclaredMembers(Type type);

        protected virtual bool MatchMemberInfo(TMemberInfo info, TData data) => info.Name == Name;

        protected virtual void ValidateInjectionMember(Type type)
        {
            if (null != MemberInfo) return;

            // TODO: 5.9.0 Implement correct error message
            var signature = "xxx";//string.Join(", ", _arguments?.FromType(t => t.Name) ?? );
            var message = $"The type {type.FullName} does not have a {typeof(TMemberInfo).Name} that takes these parameters ({signature}).";
            throw new InvalidOperationException(message);
        }

        #endregion


        #region Interface Implementations

        public virtual bool Equals(TMemberInfo other)
        {
            return MemberInfo?.Equals(other) ?? false;
        }

        #endregion


        #region Overrides

        public override bool BuildRequired => true;

        public override void AddPolicies<TContext, TPolicyList>(Type registeredType, Type mappedToType, string name, ref TPolicyList policies)
        {
            if (ReferenceEquals(Data, ResolvedValue))
            {
                foreach (var member in DeclaredMembers(mappedToType))
                {
                    if (Name != member.Name) continue;
                    if (null != MemberInfo) ThrowAmbiguousMember(MemberInfo, mappedToType);

                    MemberInfo = member;
                }
            }
            else
            {
                foreach (var member in DeclaredMembers(mappedToType))
                {
                    if (!MatchMemberInfo(member, Data)) continue;
                    if (null != MemberInfo) ThrowAmbiguousMember(MemberInfo, mappedToType);

                    MemberInfo = member;
                }
            }

            ValidateInjectionMember(mappedToType);
        }

        protected virtual void ThrowAmbiguousMember(TMemberInfo info, Type type)
        {
            // TODO: 5.9.0 Proper error message
            var signature = "xxx";//string.Join(", ", _arguments?.FromType(t => t.Name) ?? );
            var message = $"The type {type.FullName} does not have a {typeof(TMemberInfo).Name} that takes these parameters ({signature}).";
            throw new InvalidOperationException(message);
        }

        public override bool Equals(object obj)
        {
            switch (obj)
            {
                case TMemberInfo info:
                    return Equals(info);

                case IEquatable<TMemberInfo> equatable:
                    return equatable.Equals(MemberInfo);

                default:
                    return false;
            }
        }

        public override int GetHashCode()
        {
            return MemberInfo?.GetHashCode() ?? 0;
        }

        #endregion


        #region Signature matching

        protected virtual bool Matches(object data, Type match)
        {
            switch (data)
            {
                // TODO: 5.9.0 Replace with IEquatable
                case InjectionParameterValue injectionParameter:
                    return injectionParameter.MatchesType(match);

                case Type type:
                    return MatchesType(type, match);

                default:
                    return MatchesObject(data, match);
            }
        }

        protected static bool MatchesType(Type type, Type match)
        {
            if (null == type) return true;

            var typeInfo = type.GetTypeInfo();
            var matchInfo = match.GetTypeInfo();

            if (matchInfo.IsAssignableFrom(typeInfo)) return true;
            if ((typeInfo.IsArray || typeof(Array) == type) &&
               (matchInfo.IsArray || match == typeof(Array)))
                return true;

            if (typeInfo.IsGenericType && typeInfo.IsGenericTypeDefinition && matchInfo.IsGenericType &&
                typeInfo.GetGenericTypeDefinition() == matchInfo.GetGenericTypeDefinition())
                return true;

            return false;
        }

        protected static bool MatchesObject(object parameter, Type match)
        {
            var type = parameter is Type ? typeof(Type) : parameter?.GetType();

            if (null == type) return true;

            var typeInfo = type.GetTypeInfo();
            var matchInfo = match.GetTypeInfo();

            if (matchInfo.IsAssignableFrom(typeInfo)) return true;
            if ((typeInfo.IsArray || typeof(Array) == type) &&
                (matchInfo.IsArray || match == typeof(Array)))
                return true;

            if (typeInfo.IsGenericType && typeInfo.IsGenericTypeDefinition && matchInfo.IsGenericType &&
                typeInfo.GetGenericTypeDefinition() == matchInfo.GetGenericTypeDefinition())
                return true;

            return false;
        }

        #endregion
    }
}