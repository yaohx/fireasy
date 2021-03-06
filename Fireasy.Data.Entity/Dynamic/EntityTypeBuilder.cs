﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Fireasy.Common;
using Fireasy.Common.Emit;
using Fireasy.Common.Extensions;
using Fireasy.Data.Entity.Properties;

namespace Fireasy.Data.Entity.Dynamic
{
    /// <summary>
    /// 提供动态构造实体的生成器。无法继承此类。
    /// </summary>
    public sealed class EntityTypeBuilder
    {
        private static readonly MethodInfo GetValueMethod = typeof(EntityObject).GetMethods().FirstOrDefault(s => s.Name == "GetValue" && s.IsGenericMethod);
        private static readonly MethodInfo SetValueMethod = typeof(EntityObject).GetMethods().FirstOrDefault(s => s.Name == "SetValue" && s.IsGenericMethod);
        private static readonly MethodInfo RegisterMethod = typeof(PropertyUnity).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault(s => s.Name == "RegisterProperty" && !s.IsGenericMethod && s.GetParameters().Length == 2);
        private static readonly MethodInfo TypeGetTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo SetReferenceMethod = typeof(IPropertyReference).GetMethod("set_Reference");

        private readonly DynamicAssemblyBuilder assemblyBuilder;
        private List<DynamicFieldBuilder> fields;
        private Dictionary<IProperty, List<Expression<Func<Attribute>>>> validations;
        private List<Expression<Func<Attribute>>> eValidations;

        /// <summary>
        /// 初始化 <see cref="EntityTypeBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="typeName">动态类的类型名称。</param>
        /// <param name="assemblyBuilder">一个 <see cref="DynamicAssemblyBuilder"/> 容器。</param>
        /// <param name="baseType">所要继承的抽象类型，默认为 <see cref="EntityObject"/> 类。</param>
        public EntityTypeBuilder(string typeName, DynamicAssemblyBuilder assemblyBuilder = null, Type baseType = null)
        {
            TypeName = typeName;
            this.assemblyBuilder = assemblyBuilder ?? new DynamicAssemblyBuilder("<DynamicType>_" + typeName);
            InnerBuilder = this.assemblyBuilder.DefineType(TypeName, baseType: baseType ?? typeof(EntityObject));
            EntityType = InnerBuilder.UnderlyingSystemType;
            Properties = new List<IProperty>();
        }

        /// <summary>
        /// 获取实体类的名称。
        /// </summary>
        public string TypeName { get; private set; }

        /// <summary>
        /// 获取或设置映射对象。
        /// </summary>
        public EntityMappingAttribute Mapping { get; set; }

        /// <summary>
        /// 获取或设置包含的属性。
        /// </summary>
        public List<IProperty> Properties { get; set; }

        /// <summary>
        /// 获取或设置要实现的接口类型。
        /// </summary>
        [Obsolete("请使用 ImplementInterfaces 方法。")]
        public List<Type> ImplInterfaces { get; set; }

        /// <summary>
        /// 获取实体类型。
        /// </summary>
        public Type EntityType { get; private set; }

        /// <summary>
        /// 获取 <see cref="DynamicTypeBuilder"/> 对象。
        /// </summary>
        public DynamicTypeBuilder InnerBuilder { get; private set; }

        /// <summary>
        /// 为属性添加验证规则。
        /// </summary>
        /// <param name="property">要验证的属性。</param>
        /// <param name="attributes">一组 <see cref="ValidationAttribute"/> 特性。</param>
        public void DefineValidateRule(IProperty property, params Expression<Func<Attribute>>[] attributes)
        {
            Guard.ArgumentNull(attributes, "attributes");
            if (validations == null)
            {
                validations = new Dictionary<IProperty, List<Expression<Func<Attribute>>>>();
            }

            var list = validations.TryGetValue(property, () => new List<Expression<Func<Attribute>>>());
            list.AddRange(attributes);
        }

        /// <summary>
        /// 为实体添加验证规则。
        /// </summary>
        /// <param name="attributes">一组 <see cref="ValidationAttribute"/> 特性。</param>
        public void DefineValidateRule(params Expression<Func<Attribute>>[] attributes)
        {
            Guard.ArgumentNull(attributes, "attributes");
            if (eValidations == null)
            {
                eValidations = new List<Expression<Func<Attribute>>>();
            }

            eValidations.AddRange(attributes);
        }

        /// <summary>
        /// 实现多个接口类型。
        /// </summary>
        /// <param name="interfaceTypes"></param>
        public void ImplementInterfaces(params Type[] interfaceTypes)
        {
            if (interfaceTypes != null)
            {
                foreach (var type in interfaceTypes)
                {
                    InnerBuilder.ImplementInterface(type);
                }
            }
        }

        /// <summary>
        /// 为动态类添加特性。
        /// </summary>
        /// <param name="expression"></param>
        public void SetCustomAttribute(Expression<Func<Attribute>> expression)
        {
            InnerBuilder.SetCustomAttribute(expression);
        }

        /// <summary>
        /// 创建一个动态的实体类型。
        /// </summary>
        /// <returns></returns>
        public Type Create()
        {
            if (Mapping != null)
            {
                InnerBuilder.SetCustomAttribute<EntityMappingAttribute>(Mapping.TableName);
            }

            fields = new List<DynamicFieldBuilder>();

            DefineImplInterfaces();
            DefineProperties(fields);
            DefineConstructors(fields);

            DefineMetadataType();

            return InnerBuilder.CreateType();
        }

        private string GetFieldName(IProperty property)
        {
            return "<>__" + property.Name;
        }

        private void DefineImplInterfaces()
        {
            if (ImplInterfaces == null)
            {
                return;
            }
            foreach (var type in ImplInterfaces)
            {
                InnerBuilder.ImplementInterface(type);
            }
        }

        private void DefineProperties(ICollection<DynamicFieldBuilder> fields)
        {
            if (Properties == null)
            {
                return;
            }

            foreach (var property in Properties)
            {
                property.EntityType = InnerBuilder.UnderlyingSystemType;
                var fieldBuilder = InnerBuilder.DefineField(GetFieldName(property), typeof(IProperty), null, VisualDecoration.Public, CallingDecoration.Static);
                var propertyBuider = InnerBuilder.DefineProperty(property.Name, property.Type, VisualDecoration.Public, CallingDecoration.Virtual);

                var getMethod = propertyBuider.DefineGetMethod(ilCoding: bc => bc.Emitter.ldarg_0.ldsfld(fieldBuilder.FieldBuilder).callvirt(GetValueMethod.MakeGenericMethod(property.Type)).ret());
                var setMethod = propertyBuider.DefineSetMethod(ilCoding: bc => bc.Emitter.ldarg_0.ldsfld(fieldBuilder.FieldBuilder).ldarg_1.callvirt(SetValueMethod.MakeGenericMethod(property.Type)).ret());
                setMethod.DefineParameter("value");

                fields.Add(fieldBuilder);
            }
        }

        private void DefineConstructors(IList<DynamicFieldBuilder> fields)
        {
            InnerBuilder.DefineConstructor(null);
            if (Properties != null)
            {
                InnerBuilder.DefineConstructor(null, ilCoding: bc =>
                    bc.Emitter.For(0, Properties.Count, (e, i) =>
                        RegisterProperty(fields[i], e, Properties[i]))
                        .ret(),
                    calling: CallingDecoration.Static);
            }
        }

        private EmitHelper RegisterProperty(DynamicFieldBuilder fieldBuilder, EmitHelper emiter, IProperty property)
        {
            var local = emiter.DeclareLocal(property.GetType());

            var b = InnerBuilder.DefineMethod(
                "??_" + property.Name,
                typeof(IProperty), null, VisualDecoration.Private,
                CallingDecoration.Static,
#if N35
                x => 
                    { 
                        var @delegate = MakeLambdaExpression(property).Compile();
                        if (@delegate != null)
                        {
                            var bytes = @delegate.Method.GetMethodBody().GetILAsByteArray();
                            x.MethodBuilder.MethodBuilder.CreateMethodBody(bytes, bytes.Length);
                        }
                    });
#else
 x => MakeLambdaExpression(property).CompileToMethod(x.MethodBuilder.MethodBuilder));
#endif

            return emiter
                .ldtoken(InnerBuilder.UnderlyingSystemType)
                .call(TypeGetTypeFromHandle)
                .call(b.MethodBuilder)
                .stloc(local)
                .Assert(
                    property is IPropertyReference,
                    e =>
                    {
                        var p = property.As<IPropertyReference>().Reference;
                        var dynamicFieldBuilder = fields.FirstOrDefault(s => s.FieldName == GetFieldName(p));
                        if (dynamicFieldBuilder != null)
                        {
                            var field = Properties.Contains(p) ? dynamicFieldBuilder.FieldBuilder :
                                p.EntityType.GetFields(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(s => s.Name == GetFieldName(p));
                            if (field != null)
                            {
                                e.ldloc(local).ldsfld(field).call(SetReferenceMethod);
                            }
                        }
                    })
                .ldloc(local)
                .call(RegisterMethod)
                .stsfld(fieldBuilder.FieldBuilder);
        }

        /// <summary>
        /// 创建一个新对象并初始化的表达式。
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private Expression MakeMemberInitExpression(object obj)
        {
            var newExp = Expression.New(obj.GetType());
            var binds = new List<MemberBinding>();

            obj.Compare((pro, value) =>
            {
                var pp = value as IProperty;
                if (pp != null)
                {
                    return;
                }

                var propertyValue = value as PropertyValue;
                if (propertyValue != null)
                {
                    binds.Add(Expression.Bind(pro, Expression.Convert(Expression.Constant(propertyValue.GetStorageValue()), typeof(PropertyValue))));
                }
                else if (pro.PropertyType.IsValueType ||
                    pro.PropertyType == typeof(string))
                {
                    binds.Add(pro.PropertyType.IsNullableType()
                        ? Expression.Bind(pro, Expression.Convert(Expression.Constant(value), pro.PropertyType))
                        : Expression.Bind(pro, Expression.Constant(value)));
                }
                else if (pro.PropertyType == typeof(Type))
                {
                    binds.Add(Expression.Bind(pro, Expression.Constant(value, typeof(Type))));
                }
                else
                {
                    binds.Add(Expression.Bind(pro, MakeMemberInitExpression(value)));
                }
            });

            return Expression.MemberInit(newExp, binds);
        }

        /// <summary>
        /// 根据一个 <see cref="IProperty"/> 创建一个 <see cref="LambdaExpression"/>。
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private LambdaExpression MakeLambdaExpression(IProperty property)
        {
            return Expression.Lambda<Func<IProperty>>(MakeMemberInitExpression(property));
        }

        /// <summary>
        /// 定义 MetadataTypeAttribute 类。
        /// </summary>
        private void DefineMetadataType()
        {
            if (validations.IsNullOrEmpty() && eValidations.IsNullOrEmpty())
            {
                return;
            }

            var metadataType = assemblyBuilder.DefineType("<Metadata>__" + TypeName);

            if (validations != null)
            {
                foreach (var property in validations.Keys)
                {
                    var propertyBuilder = metadataType.DefineProperty(property.Name, typeof(IProperty));
                    propertyBuilder.DefineGetSetMethods();

                    foreach (var expression in validations[property])
                    {
                        propertyBuilder.SetCustomAttribute(expression);
                    }
                }
            }

            if (eValidations != null)
            {
                foreach (var expression in eValidations)
                {
                    metadataType.SetCustomAttribute(expression);
                }
            }

            InnerBuilder.SetCustomAttribute<MetadataTypeAttribute>(metadataType.CreateType());
        }
    }
}