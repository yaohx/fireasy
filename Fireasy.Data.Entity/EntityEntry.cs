﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Fireasy.Data.Entity
{
    /// <summary>
    /// 用于记录实体的属性的数据。
    /// </summary>
    [Serializable]
    internal sealed class EntityEntry
    {
        private PropertyValue oldValue;
        private PropertyValue newValue;

        /// <summary>
        /// 获取或设置实体值是否已修改。
        /// </summary>
        internal bool IsModified { get; set; }

        /// <summary>
        /// 重置修改状态。
        /// </summary>
        internal void Reset()
        {
            if (IsModified && newValue != null)
            {
                oldValue = newValue.Clone();
                newValue = null;
                IsModified = false;
            }
        }

        /// <summary>
        /// 使用指定的值进行修改。
        /// </summary>
        /// <param name="value"></param>
        internal void Modify(PropertyValue value)
        {
            newValue = value;
            IsModified = true;
        }

        /// <summary>
        /// 标识已被修改。
        /// </summary>
        internal void Modify()
        {
            if (!IsModified)
            {
                newValue = oldValue;
                IsModified = true;
            }
        }

        /// <summary>
        /// 获取当前值。如果正在修改，则返回新值，否则为旧值。
        /// </summary>
        /// <returns></returns>
        internal PropertyValue GetCurrentValue()
        {
            if (IsModified)
            {
                return newValue;
            }

            return newValue ?? oldValue;
        }

        /// <summary>
        /// 获取旧值。
        /// </summary>
        /// <returns></returns>
        internal PropertyValue GetOldValue()
        {
            return oldValue;
        }

        /// <summary>
        /// 初始化，设置旧值。
        /// </summary>
        /// <param name="value"></param>
        internal void Initializate(PropertyValue value)
        {
            oldValue = value;
        }

        /// <summary>
        /// 使用旧值初始化一个 <see cref="EntityEntry"/>。
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static EntityEntry InitByOldValue(PropertyValue value)
        {
            return new EntityEntry { oldValue = value };
        }

        /// <summary>
        /// 使用新值初始化一个 <see cref="EntityEntry"/>。
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static EntityEntry InitByNewValue(PropertyValue value)
        {
            return new EntityEntry { newValue = value, IsModified = true };
        }
    }
}