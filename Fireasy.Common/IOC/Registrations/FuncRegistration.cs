﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq.Expressions;

namespace Fireasy.Common.Ioc.Registrations
{
    internal class FuncRegistration<TService> : AbstractRegistration where TService : class
    {
        private readonly Func<object> m_instanceCreator;

        internal FuncRegistration(Func<object> instanceCreator)
            : base(typeof(TService))
        {
            m_instanceCreator = instanceCreator;
        }

        internal override Expression BuildExpression()
        {
            return Expression.Invoke(Expression.Constant(m_instanceCreator), new Expression[0]);
        }
    }
}
