﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Fireasy.Common.Extensions;
using Fireasy.Data.Entity.Extensions;
using Fireasy.Data.Entity.Linq.Expressions;
using Fireasy.Data.Entity.Metadata;
using Fireasy.Data.Entity.Properties;
using System.Reflection;
using Fireasy.Data.Converter;
using Fireasy.Data.Extensions;
using System.Data;
using Fireasy.Common.Aop;
using Fireasy.Data.Syntax;

namespace Fireasy.Data.Entity.Linq.Translators
{
    internal sealed class QueryUtility
    {
        internal static string FIRST_ENTITY_KEY = "FirstEntity";

        internal static LambdaExpression GetAggregator(Type expectedType, Type actualType)
        {
            var actualElementType = actualType.GetEnumerableElementType();
            if (!expectedType.IsAssignableFrom(actualType))
            {
                var expectedElementType = expectedType.GetEnumerableElementType();
                var p = Expression.Parameter(actualType, "p");
                Expression body = null;
                if (expectedType.IsAssignableFrom(actualElementType))
                {
                    body = Expression.Call(typeof(Enumerable), "SingleOrDefault", new[] { actualElementType }, p);
                }
                else if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(IQueryable<>))
                {
                    body = Expression.Call(typeof(Queryable), "AsQueryable", new[] { expectedElementType }, CastElementExpression(expectedElementType, p));
                }
                else if (expectedType.IsArray && expectedType.GetArrayRank() == 1)
                {
                    body = Expression.Call(typeof(Enumerable), "ToArray", new[] { expectedElementType }, CastElementExpression(expectedElementType, p));
                }
                else if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(EntitySet<>))
                {
                    body = Expression.Call(typeof(Entity.EnumerableExtension), "ToEntitySet", new[] { expectedElementType }, CastElementExpression(expectedElementType, p));
                }
                else if (expectedType.IsAssignableFrom(typeof(List<>).MakeGenericType(actualElementType)))
                {
                    body = Expression.Call(typeof(Enumerable), "ToList", new[] { expectedElementType }, CastElementExpression(expectedElementType, p));
                }
                else
                {
                    var ci = expectedType.GetConstructor(new[] { actualType });
                    if (ci != null)
                    {
                        body = Expression.New(ci, p);
                    }
                }
                if (body != null)
                {
                    return Expression.Lambda(body, p);
                }
            }
            return null;
        }

        /// <summary>
        /// 判断表达式是否可用为 Column 使用。
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        internal static bool CanBeColumnExpression(Expression expression)
        {
            switch (expression.NodeType)
            {
                case (ExpressionType)DbExpressionType.Column:
                case (ExpressionType)DbExpressionType.Scalar:
                case (ExpressionType)DbExpressionType.Exists:
                case (ExpressionType)DbExpressionType.AggregateSubquery:
                case (ExpressionType)DbExpressionType.Aggregate:
                    return true;
                default:
                    return false;
            }
        }

        private static Expression CastElementExpression(Type expectedElementType, Expression expression)
        {
            var elementType = expression.Type.GetEnumerableElementType();
            if (expectedElementType != elementType &&
                (expectedElementType.IsAssignableFrom(elementType) ||
                  elementType.IsAssignableFrom(expectedElementType)))
            {
                return Expression.Call(typeof(Enumerable), "Cast", new[] { expectedElementType }, expression);
            }
            return expression;
        }

        internal static ProjectionExpression GetTableQuery(EntityMetadata entity)
        {
            var tableAlias = new TableAlias();
            var selectAlias = new TableAlias();
            var entityType = entity.EntityType;
            var table = new TableExpression(tableAlias, entity.TableName, entityType);

            var projector = GetTypeProjection(table, entity);
            var pc = ColumnProjector.ProjectColumns(CanBeColumnExpression, projector, null, selectAlias, tableAlias);

            var proj = new ProjectionExpression(
                new SelectExpression(selectAlias, pc.Columns, table, null),
                pc.Projector
                );

            return (ProjectionExpression)ApplyPolicy(proj, entityType);
        }

        internal static Expression GetTypeProjection(Expression root, EntityMetadata entity)
        {
            var entityType = entity.EntityType;
            var bindings = new List<MemberBinding>();
            //获取实体中定义的所有依赖属性
            var properties = PropertyUnity.GetLoadedProperties(entityType, true);
            foreach (var property in properties)
            {
                var mbrExpression = GetMemberExpression(root, property);
                if (mbrExpression != null)
                {
                    bindings.Add(Expression.Bind(property.Info.ReflectionInfo, mbrExpression));
                }
            }
            return new EntityExpression(entity, Expression.MemberInit(Expression.New(entityType), bindings));
        }

        internal static Expression GetMemberExpression(Expression root, IProperty property)
        {
            var relationProprety = property as RelationProperty;
            if (relationProprety != null)
            {
                //所关联的实体类型
                var relMetadata = EntityMetadataUnity.GetEntityMetadata(relationProprety.RelationType);
                var projection = GetTableQuery(relMetadata);

                Expression parentExp = null, childExp = null;
                var ship = RelationshipUnity.GetRelationship(relationProprety);
                if (ship.ThisType != relationProprety.EntityType)
                {
                    parentExp = projection.Projector;
                    childExp = root;
                }
                else
                {
                    parentExp = root;
                    childExp = projection.Projector;
                }

                Expression where = null;
                for (int i = 0, n = ship.Keys.Count; i < n; i++)
                {
                    var equal = GetMemberExpression(parentExp, ship.Keys[i].ThisProperty)
                        .Equal(GetMemberExpression(childExp, ship.Keys[i].OtherProperty));
                    where = (where != null) ? Expression.And(where, equal) : equal;
                }

                var newAlias = new TableAlias();
                var pc = ColumnProjector.ProjectColumns(CanBeColumnExpression, projection.Projector, null, newAlias, projection.Select.Alias);

                var aggregator = GetAggregator(property.Type, typeof(IEnumerable<>).MakeGenericType(pc.Projector.Type));
                var result = new ProjectionExpression(
                    new SelectExpression(newAlias, pc.Columns, projection.Select, where),
                    pc.Projector, aggregator
                    );

                return ApplyPolicy(result, property.Info.ReflectionInfo);
            }

            var table = root as TableExpression;
            if (table != null)
            {
                var sqProperty = property as SubqueryProperty;
                if (sqProperty != null)
                {
                    return new SubqueryColumnExpression(property.Type, table.Alias, property.Info.FieldName, sqProperty.Subquery);
                }
                else if (property is ISavedProperty)
                {
                    return new ColumnExpression(property.Type, table.Alias, property.Name, property.Info);
                }
            }
            return QueryBinder.BindMember(root, property.Info.ReflectionInfo);
        }

        internal static Expression GetDeleteExpression(EntityMetadata metadata, LambdaExpression predicate, bool replace)
        {
            var table = new TableExpression(new TableAlias(), metadata.TableName, metadata.EntityType);
            Expression where = null;

            if (predicate != null)
            {
                if (replace)
                {
                    var row = GetTypeProjection(table, metadata);
                    where = DbExpressionReplacer.Replace(predicate.Body, predicate.Parameters[0], row);
                }
                else
                {
                    where = predicate.Body;
                }
            }

            return new DeleteCommandExpression(table, where);
        }

        internal static Expression GetFakeDeleteExpression(EntityMetadata metadata, LambdaExpression predicate)
        {
            var table = new TableExpression(new TableAlias(), metadata.TableName, metadata.EntityType);
            Expression where = null;

            var delflag = (ColumnExpression)GetMemberExpression(table, metadata.DeleteProperty);
            var assignments = new List<ColumnAssignment>
                {
                    new ColumnAssignment(delflag, Expression.Constant(true))
                };

            if (predicate != null)
            {
                var row = GetTypeProjection(table, metadata);
                where = DbExpressionReplacer.Replace(predicate.Body, predicate.Parameters[0], row);
            }

            return new UpdateCommandExpression(table, where, assignments);
        }

        internal static Expression GetUpdateExpression(Expression instance, LambdaExpression predicate)
        {
            if (instance is ParameterExpression)
            {
                return GetUpdateExpressionByParameter((ParameterExpression)instance, predicate);
            }
            else if (instance is ConstantExpression)
            {
                return GetUpdateExpressionByEntity((ConstantExpression)instance, predicate);
            }

            var lambda = instance as LambdaExpression;
            if (lambda == null && instance.NodeType == ExpressionType.Quote)
            {
                lambda = ((UnaryExpression)instance).Operand as LambdaExpression;
            }

            if (lambda != null)
            {
                return GetUpdateExpressionByCalculator(lambda, predicate);
            }

            return null;
        }

        internal static LambdaExpression GetPrimaryKeyExpression(ParameterExpression parExp)
        {
            var metadata = EntityMetadataUnity.GetEntityMetadata(parExp.Type);
            var table = new TableExpression(new TableAlias(), metadata.TableName, parExp.Type);
            var primaryKeys = PropertyUnity.GetPrimaryProperties(parExp.Type);
            Expression where = null;

            foreach (var pk in primaryKeys)
            {
                var eq = GetMemberExpression(table, pk).Equal(Expression.MakeMemberAccess(parExp, pk.Info.ReflectionInfo));
                where = where == null ? eq : Expression.And(where, eq);
            }

            return Expression.Lambda(where, parExp);
        }

        private static Expression GetUpdateExpressionByEntity(ConstantExpression constant, LambdaExpression predicate)
        {
            var entity = constant.Value as IEntity;
            var entityType = entity.EntityType;
            var metadata = EntityMetadataUnity.GetEntityMetadata(entityType);
            var table = new TableExpression(new TableAlias(), metadata.TableName, metadata.EntityType);
            Expression where = null;

            if (predicate != null)
            {
                var row = GetTypeProjection(table, metadata);
                where = DbExpressionReplacer.Replace(predicate.Body, predicate.Parameters[0], row);
            }

            return new UpdateCommandExpression(table, where, GetUpdateArguments(table, entity));
        }

        private static Expression GetUpdateExpressionByParameter(ParameterExpression parExp, LambdaExpression predicate)
        {
            var metadata = EntityMetadataUnity.GetEntityMetadata(parExp.Type);
            var table = new TableExpression(new TableAlias(), metadata.TableName, parExp.Type);
            return new UpdateCommandExpression(table, predicate.Body, GetUpdateArguments(table, parExp));
        }

        private static Expression GetUpdateExpressionByCalculator(LambdaExpression lambda, LambdaExpression predicate)
        {
            var initExp = lambda.Body as MemberInitExpression;
            var newExp = initExp.NewExpression;
            var entityType = newExp.Type;
            var metadata = EntityMetadataUnity.GetEntityMetadata(entityType);
            var table = new TableExpression(new TableAlias(), metadata.TableName, metadata.EntityType);
            Expression where = null;
            var row = GetTypeProjection(table, metadata);

            if (predicate != null)
            {
                where = DbExpressionReplacer.Replace(predicate.Body, predicate.Parameters[0], row);
            }

            var list = new List<MemberAssignment>();
            foreach (MemberAssignment ass in initExp.Bindings)
            {
                list.Add(Expression.Bind(ass.Member, DbExpressionReplacer.Replace(ass.Expression, lambda.Parameters[0], row)));
            }

            return new UpdateCommandExpression(table, where, GetUpdateArguments(table, list));
        }

        internal static Expression GetInsertExpression(ISyntaxProvider syntax, Expression instance)
        {
            InsertCommandExpression insertExp;
            var entityType = instance.Type;
            Func<TableExpression, IEnumerable<ColumnAssignment>> func;
            if (instance is ParameterExpression)
            {
                var parExp = (ParameterExpression)instance;
                func = new Func<TableExpression, IEnumerable<ColumnAssignment>>(t => GetInsertArguments(syntax, t, parExp));
            }
            else
            {
                var entity = instance.As<ConstantExpression>().Value as IEntity;
                func = new Func<TableExpression, IEnumerable<ColumnAssignment>>(t => GetInsertArguments(syntax, t, entity));
            }

            var metadata = EntityMetadataUnity.GetEntityMetadata(entityType);
            var table = new TableExpression(new TableAlias(), metadata.TableName, entityType);
            insertExp = new InsertCommandExpression(table, func(table));

            insertExp.WithAutoIncrement = !string.IsNullOrEmpty(syntax.IdentitySelect) &&
                HasAutoIncrement(instance.Type);

            return insertExp;
        }

        private static bool HasAutoIncrement(Type entityType)
        {
            return PropertyUnity.GetPrimaryProperties(entityType).Any(s => s.Info.GenerateType == IdentityGenerateType.AutoIncrement);
        }

        private static IEnumerable<ColumnAssignment> GetUpdateArguments(TableExpression table, IEntity entity)
        {
            var entityType = entity.EntityType;
            var properties = GetModifiedProperties(entity);

            return properties
                .Select(m => new ColumnAssignment(
                       (ColumnExpression)GetMemberExpression(table, m),
                       Expression.Constant(GetConvertableValue(entity, m))
                       )).ToList();
        }

        private static IEnumerable<ColumnAssignment> GetUpdateArguments(TableExpression table, ParameterExpression parExp)
        {
            IEnumerable<IProperty> properties = null;
            IEntity firstEntity = null;
            if ((firstEntity = GetFirstEntityFromContext()) != null)
            {
                properties = GetModifiedProperties(firstEntity);
            }

            return properties
                .Select(m => new ColumnAssignment(
                       (ColumnExpression)GetMemberExpression(table, m),
                       Expression.MakeMemberAccess(parExp, m.Info.ReflectionInfo)
                       ));
        }

        private static IEnumerable<ColumnAssignment> GetUpdateArguments(TableExpression table, IEnumerable<MemberAssignment> bindings)
        {
            return from m in bindings
                   let property = PropertyUnity.GetProperty(table.Type, m.Member.Name)
                   select new ColumnAssignment(
                       (ColumnExpression)GetMemberExpression(table, property),
                       m.Expression
                       );
        }

        /// <summary>
        /// 返回插入具体实体时的 <see cref="ColumnAssignment"/> 集合。
        /// </summary>
        /// <param name="syntax"></param>
        /// <param name="table"></param>
        /// <param name="entity">具体的实体。</param>
        /// <returns></returns>
        private static IEnumerable<ColumnAssignment> GetInsertArguments(ISyntaxProvider syntax, TableExpression table, IEntity entity)
        {
            var entityType = entity.EntityType;
            var properties = GetModifiedProperties(entity);

            var assignments = properties
                .Select(m => new ColumnAssignment(
                       (ColumnExpression)GetMemberExpression(table, m),
                       Expression.Constant(GetConvertableValue(entity, m))
                       )).ToList();

            assignments.AddRange(GetAssignmentsForPrimaryKeys(syntax, table, null, entity));

            return assignments;
        }

        /// <summary>
        /// 返回插入具体实体时的 <see cref="ColumnAssignment"/> 集合。
        /// </summary>
        /// <param name="syntax"></param>
        /// <param name="table"></param>
        /// <param name="parExp"></param>
        /// <returns></returns>
        private static IEnumerable<ColumnAssignment> GetInsertArguments(ISyntaxProvider syntax, TableExpression table, ParameterExpression parExp)
        {
            IEnumerable<IProperty> properties = null;
            IEntity firstEntity = null;

            if ((firstEntity = GetFirstEntityFromContext()) != null)
            {
                properties = GetModifiedProperties(firstEntity);
            }

            var assignments = properties
                .Select(m => new ColumnAssignment(
                       (ColumnExpression)GetMemberExpression(table, m),
                       Expression.MakeMemberAccess(parExp, m.Info.ReflectionInfo)
                       )).ToList();

            assignments.AddRange(GetAssignmentsForPrimaryKeys(syntax, table, parExp, null));

            return assignments;
        }

        private static IEnumerable<ColumnAssignment> GetAssignmentsForPrimaryKeys(ISyntaxProvider syntax, TableExpression table, ParameterExpression parExp, IEntity entity)
        {
            var entityType = parExp != null ? parExp.Type : entity.EntityType;
            var ex = entity as IEntityStatefulExtension;
            var assignments = new List<ColumnAssignment>();
            foreach (var p in PropertyUnity.GetPrimaryProperties(entityType))
            {
                var valueExp = GetPrimaryValueExpression(syntax, table, parExp, entity, p);

                if (valueExp != null)
                {
                    var columnExp = (ColumnExpression)GetMemberExpression(table, p);
                    assignments.Add(new ColumnAssignment(columnExp, valueExp));
                }
            }

            return assignments;
        }

        private static Expression ApplyPolicy(Expression expression, MemberInfo member)
        {
            if (TranslateScope.Current != null)
            {
                var policy = TranslateScope.Current.Context as IQueryPolicy;
                if (policy != null)
                {
                    return policy.ApplyPolicy(expression, member);
                }
            }

            return expression;
        }

        /// <summary>
        /// 获取实体可插入或更新的非主键属性列表。
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private static IEnumerable<IProperty> GetModifiedProperties(IEntity entity)
        {
            var entityType = entity.EntityType;
            var properties = PropertyUnity.GetPersistentProperties(entityType)
                .Where(m => !m.Info.IsPrimaryKey || 
                    (m.Info.IsPrimaryKey && m.Info.GenerateType == IdentityGenerateType.None));

            var entityEx = entity as IEntityStatefulExtension;

            //返回未定义默认值的所有属性
            if (entityType.IsNotImplAOPType() || entityEx == null || entity.EntityState == EntityState.Unchanged)
            {
                return properties.Where(m => !entity.InternalGetValue(m).IsNullOrEmpty());
            }
            else
            {
                return properties.Where(m => entityEx.IsModified(m.Name));
            }
        }

        /// <summary>
        /// 获取主键值的表达式。
        /// </summary>
        /// <param name="syntax"></param>
        /// <param name="table"></param>
        /// <param name="parExp"></param>
        /// <param name="entity"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        private static Expression GetPrimaryValueExpression(ISyntaxProvider syntax, TableExpression table, ParameterExpression parExp, IEntity entity, IProperty property)
        {
            //如果不支持自增量，则使用 IGenerateProvider生成
            if (property.Info.GenerateType == IdentityGenerateType.Generator ||
                (property.Info.GenerateType == IdentityGenerateType.AutoIncrement && 
                string.IsNullOrEmpty(syntax.IdentitySelect)))
            {
                return new GeneratorExpression(table, parExp ?? (Expression)Expression.Constant(entity), property);
            }

            return null;
        }

        /// <summary>
        /// 获取受 <see cref="IValueConverter"/> 支持的数据转换值。
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        private static object GetConvertableValue(IEntity entity, IProperty property)
        {
            var value = entity.InternalGetValue(property);
            if (!value.IsNullOrEmpty())
            {
                var converter = ConvertManager.GetConverter(property.Type);
                if (converter != null)
                {
                    var storageValue = value.GetStorageValue();
                    var convertValue = converter.ConvertTo(storageValue, (DbType)property.Info.DataType);
                    return convertValue;
                }

                return value.GetStorageValue();
            }

            return property.Type.GetDefaultValue();
        }

        /// <summary>
        /// 在批处理的指令中获取第一个实体对象。
        /// </summary>
        /// <returns></returns>
        private static IEntity GetFirstEntityFromContext()
        {
            IEntity firstEntity;
            if (TranslateScope.Current != null &&
                (firstEntity = TranslateScope.Current.GetData<object>(FIRST_ENTITY_KEY) as IEntity) != null)
            {
                return firstEntity;
            }

            return null;
        }
    }
}