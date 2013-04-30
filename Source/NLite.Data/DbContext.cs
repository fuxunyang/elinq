﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;

namespace NLite.Data
{
    using Mapping;
    using NLite.Data.Linq.Internal;
    using NLite.Reflection;

    /// <summary>
    /// Db上下文
    /// </summary>
    public class DbContext : BooleanDisposable, IDbContext, IUnitOfWork, IQueryProvider
    {
        /// <summary>
        /// 当前DbContext
        /// </summary>
        public static IDbContext Current
        {
            get { return UnitOfWork.Current as IDbContext; }
        }

        /// <summary>
        /// 得到DbContext
        /// </summary>
        /// <param name="dbConfigurationName"></param>
        /// <returns></returns>
        public static IDbContext Get(string dbConfigurationName)
        {
            return UnitOfWork.Get(dbConfigurationName) as IDbContext;
        }
        /// <summary>
        /// 得到DbConfiguration对象
        /// </summary>
        public DbConfiguration DbConfiguration { get; private set; }
        /// <summary>
        /// DbConfiguration 名称
        /// </summary>
        public string DbConfigurationName { get { return DbConfiguration.Name; } }
        /// <summary>
        /// 得到连接对象
        /// </summary>
        public DbConnection Connection { get; private set; }
        /// <summary>
        /// 得到DbHelper对象
        /// </summary>
        public IDbHelper DbHelper { get; private set; }

        ///// <summary>
        ///// 得到存储过程助手对象
        ///// </summary>
        //public IDbHelper SpHelper { get; private set; }

        private InternalDbContext InnerContext;
        /// <summary>
        /// 根据dbConfiguration 创建DbContext对象
        /// </summary>
        /// <param name="dbConfiguration"></param>
        public DbContext(DbConfiguration dbConfiguration)
        {
            Guard.NotNull(dbConfiguration, "dbConfiguration");
            InnerContext = new InternalDbContext(dbConfiguration);
            DbConfiguration = dbConfiguration;
            Connection = InnerContext.Connection;
            DbHelper = InnerContext.DbHelper;
            InitializeDbSets();
        }
        /// <summary>
        /// 根据dbConfigurationName 创建DbContext对象
        /// </summary>
        /// <param name="dbConfigurationName"></param>
        public DbContext(string dbConfigurationName) : this(DbConfiguration.Get(dbConfigurationName)) { }

        /// <summary>
        /// 得到对应的DbSet对象
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <returns></returns>
        public IDbSet<TEntity> Set<TEntity>()
        {
            return InnerContext.Set<TEntity>();
        }

        IRepository<TEntity> IUnitOfWork.CreateRepository<TEntity>()
        {
            return InnerContext.Set<TEntity>();
        }

        /// <summary>
        /// 启用Ado.net事务
        /// </summary>
        /// <param name="action"></param>
        /// <param name="isolationLevel"></param>
        public void UsingTransaction(Action action)
        {
            InnerContext.UsingTransaction(action, IsolationLevel.Unspecified);
        }

        /// <summary>
        /// 启用Ado.net事务
        /// </summary>
        /// <param name="action"></param>
        /// <param name="isolationLevel"></param>
        public void UsingTransaction(Action action, IsolationLevel isolationLevel)
        {
            InnerContext.UsingTransaction(action, isolationLevel);
        }

        class MemberInitializer
        {
            public IEntityMapping EntityModel;
            public Setter Setter;
            public ConstructorHandler DbSetConstrctor;

            public void Init(DbContext dbContext)
            {
                var set = DbSetConstrctor(dbContext.InnerContext, EntityModel);
                Setter(dbContext, set);
                lock (dbContext.InnerContext.dbSetTable)
                    dbContext.InnerContext.dbSetTable[EntityModel.EntityType] = set as IDbSet;
            }
        }
        static Dictionary<string, List<MemberInitializer>> MemberInitializersGroup = new Dictionary<string, List<MemberInitializer>>();
        private void InitializeDbSets()
        {
            var type = GetType();
            if (type == typeof(InternalDbContext)) return;

            List<MemberInitializer> items;
            if (!MemberInitializersGroup.TryGetValue(DbConfiguration.Name, out items))
            {
                items = new List<MemberInitializer>();
                foreach (var item in type.GetFields().Where(p => typeof(IDbSet).IsAssignableFrom(p.FieldType)))
                {
                    var initiazer = new MemberInitializer();
                    initiazer.EntityModel = DbConfiguration.GetMapping(ReflectionHelper.GetElementType(item.FieldType));
                    initiazer.Setter = item.GetSetter();
                    initiazer.DbSetConstrctor = typeof(DbSet<>).MakeGenericType(initiazer.EntityModel.EntityType).GetConstructors().FirstOrDefault().GetCreator();
                    items.Add(initiazer);
                }
                foreach (var item in type.GetProperties().Where(p => typeof(IDbSet).IsAssignableFrom(p.PropertyType)).Where(p => p.CanWrite))
                {
                    var initiazer = new MemberInitializer();
                    initiazer.EntityModel = DbConfiguration.GetMapping(ReflectionHelper.GetElementType(item.PropertyType));
                    initiazer.Setter = item.GetSetter();
                    initiazer.DbSetConstrctor = typeof(DbSet<>).MakeGenericType(initiazer.EntityModel.EntityType).GetConstructors().FirstOrDefault().GetCreator();
                    items.Add(initiazer);
                }
                lock (MemberInitializersGroup)
                    MemberInitializersGroup[DbConfiguration.Name] = items;
            }

            foreach (var item in items)
                item.Init(this);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                InnerContext.Dispose();
        }


        IDbSet IDbContext.Set(Type type)
        {
            Guard.NotNull(type, "type");
            return InnerContext.GetDbSet(type);
        }

        IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
        {
            return (InnerContext as IQueryProvider).CreateQuery<TElement>(expression);
        }

        IQueryable IQueryProvider.CreateQuery(Expression expression)
        {
            return (InnerContext as IQueryProvider).CreateQuery(expression);
        }

        TResult IQueryProvider.Execute<TResult>(Expression expression)
        {
            return (InnerContext as IQueryProvider).Execute<TResult>(expression);
        }

        object IQueryProvider.Execute(Expression expression)
        {
            return (InnerContext as IQueryProvider).Execute(expression);
        }
    }
}
