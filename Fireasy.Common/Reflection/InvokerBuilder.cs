﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Fireasy.Common.Reflection
{
    /// <summary>
    /// 执行器的构造器。
    /// </summary>
    internal class InvokerBuilder
    {
        internal static Expression BuildMethodCall(MethodBase method, Type type, ParameterExpression targetParameterExpression, ParameterExpression argsParameterExpression)
        {
            var parametersInfo = method.GetParameters();

            var argsExpression = new Expression[parametersInfo.Length];
            var refParameterMap = new List<ByRefParameter>();

            for (int i = 0; i < parametersInfo.Length; i++)
            {
                var parameter = parametersInfo[i];
                var parameterType = parameter.ParameterType;
                var isByRef = false;
                if (parameterType.IsByRef)
                {
                    parameterType = parameterType.GetElementType();
                    isByRef = true;
                }

                var indexExpression = Expression.Constant(i);

                var paramAccessorExpression = Expression.ArrayIndex(argsParameterExpression, indexExpression);

                Expression argExpression;

                if (parameterType.IsValueType)
                {
                    var ensureValueTypeNotNull = Expression.Coalesce(paramAccessorExpression, Expression.New(parameterType));

                    argExpression = EnsureCastExpression(ensureValueTypeNotNull, parameterType);
                }
                else
                {
                    argExpression = EnsureCastExpression(paramAccessorExpression, parameterType);
                }

#if !N35
                if (isByRef)
                {
                    var variable = Expression.Variable(parameterType);
                    refParameterMap.Add(new ByRefParameter
                        {
                            Value = argExpression,
                            Variable = variable,
                            IsOut = parameter.IsOut
                        });

                    argExpression = variable;
                }
#endif

                argsExpression[i] = argExpression;
            }

            Expression callExpression;
            if (method.IsConstructor)
            {
                callExpression = Expression.New((ConstructorInfo)method, argsExpression);
            }
            else if (method.IsStatic)
            {
                callExpression = Expression.Call((MethodInfo)method, argsExpression);
            }
            else
            {
                var readParameter = EnsureCastExpression(targetParameterExpression, method.DeclaringType);

                callExpression = Expression.Call(readParameter, (MethodInfo)method, argsExpression);
            }

            if (method is MethodInfo)
            {
                var m = (MethodInfo)method;
                if (m.ReturnType != typeof(void))
                {
                    callExpression = EnsureCastExpression(callExpression, type);
                }
                else
                {
#if !N35
                    callExpression = Expression.Block(callExpression, Expression.Constant(null));
#else
                    callExpression = EnsureCastExpression(callExpression, typeof(void));
#endif
                }
            }
            else
            {
                callExpression = EnsureCastExpression(callExpression, type);
            }

#if !N35
            if (refParameterMap.Count > 0)
            {
                var variableExpressions = new List<ParameterExpression>();
                var bodyExpressions = new List<Expression>();
                foreach (ByRefParameter p in refParameterMap)
                {
                    if (!p.IsOut)
                    {
                        bodyExpressions.Add(Expression.Assign(p.Variable, p.Value));
                    }

                    variableExpressions.Add(p.Variable);
                }

                bodyExpressions.Add(callExpression);

                callExpression = Expression.Block(variableExpressions, bodyExpressions);
            }
#endif

            return callExpression;
        }

        private static Expression EnsureCastExpression(Expression expression, Type targetType)
        {
            var expressionType = expression.Type;

            if (expressionType == targetType ||
                (!expressionType.IsValueType && targetType.IsAssignableFrom(expressionType)))
            {
                return expression;
            }

            return Expression.Convert(expression, targetType);
        }

        private class ByRefParameter
        {
            public Expression Value { get; set; }
            public ParameterExpression Variable { get; set; }
            public bool IsOut { get; set; }
        }
    }
}
