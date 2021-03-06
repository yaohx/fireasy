﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Fireasy.Web.Http
{
    /// <summary>
    /// 标识在发生异常时采取何种替代行为。无法继承此类。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple=false)]
    public sealed class ExceptionBehaviorAttribute : Attribute
    {
        /// <summary>
        /// 初始化 <see cref="ExceptionBehaviorAttribute"/> 类的新实例。
        /// </summary>
        /// <param name="message">错误信息。</param>
        public ExceptionBehaviorAttribute(string message)
        {
            Message = message;
        }

        /// <summary>
        /// 初始化 <see cref="ExceptionBehaviorAttribute"/> 类的新实例。
        /// </summary>
        /// <param name="message">是否返回空数组。</param>
        public ExceptionBehaviorAttribute(bool isEmpty)
        {
            EmptyArray = isEmpty;
        }

        /// <summary>
        /// 获取或设置异常的替代信息。
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 获取或设置是否以空数组作为替代。
        /// </summary>
        public bool EmptyArray { get; set; }
    }
}
