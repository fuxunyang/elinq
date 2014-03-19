﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NLite.Data.Common;
using NLite.Data.Dialect;
using NLite.Data.Driver;
using NLite.Data.Linq;
using NLite.Data.Linq.Expressions;
using NLite.Data.Linq.Internal;
using NLite.Data.Linq.Translation;
using NLite.Data.Mapping;
using NLite.Reflection;
using System.Runtime.CompilerServices;

namespace NLite.Data
{
    /// <summary>
    /// 
    /// </summary>
    partial class InternalDbContext : ConnectionHost, IDbContext, IUnitOfWork, IQueryProvider
    {
        internal readonly IDialect Dialect;
        internal readonly IDriver Driver;
        internal readonly IDbExpressionBuilder ExpressionBuilder;
        internal ISqlLog Log;
        internal DbConfiguration dbConfiguration;
        internal Dictionary<Type, IDbSet> dbSetTable = new Dictionary<Type, IDbSet>();

        public InternalDbContext(DbConfiguration dbConfiguration)
            : this(dbConfiguration, dbConfiguration.CreateDbConnection(), true)
        {
        }

        internal InternalDbContext(DbConfiguration dbConfiguration, DbConnection conn)
            : this(dbConfiguration, conn, false)
        {
        }

        private InternalDbContext(DbConfiguration dbConfiguration, DbConnection conn, bool hasSelfCreateConnection)
        {
            Driver = dbConfiguration.Driver;
            Dialect = dbConfiguration.Dialect;
            this.dbConfiguration = dbConfiguration;
            connection = conn;
            Operations = new Dictionary<MemberInfo, List<LambdaExpression>>();
            this.ExpressionBuilder = dbConfiguration.Option.DbExpressionBuilder;
            Log = dbConfiguration.sqlLogger();
            HasSelfCreateConnection = hasSelfCreateConnection;
        }
        public DbConnection Connection { get { return connection; } }

        public DbConfiguration DbConfiguration { get { return dbConfiguration; } }

        public string DbConfigurationName
        {
            get { return dbConfiguration.Name; }
        }

        private IDbHelper dbHelper;
        public IDbHelper DbHelper
        {
            get
            {
                if (dbHelper == null)
                    dbHelper = new DbHelper { CommandType = CommandType.Text, connection = this.connection, dbConfiguration = this.dbConfiguration, Driver = this.Driver, HasSelfCreateConnection = false };
                return dbHelper;
            }
        }

        private IDbHelper spHelper;
        public IDbHelper SpHelper
        {
            get
            {
                if (spHelper == null)
                    spHelper = new DbHelper { CommandType = CommandType.StoredProcedure, connection = this.connection, dbConfiguration = this.dbConfiguration, Driver = this.Driver, HasSelfCreateConnection = false };
                return spHelper;
            }
        }

        public void UsingTransaction(Action action)
        {
            UsingTransaction(action, IsolationLevel.Unspecified);
        }
        public void UsingTransaction(Action action, IsolationLevel isolationLevel)
        {
            Guard.NotNull(action, "action");

            var conn = connection;
            try
            {
                if (conn.State != ConnectionState.Open)
                    conn.Open();
            }
            catch
            {
                throw;
            }

            DbTransaction tx = null;
            try
            {
                tx = conn.BeginTransaction(isolationLevel);
            }
            catch
            {
                throw;
            }

            try
            {
                action();
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

        }

        IDbSet GetDbSet(IEntityMapping entity)
        {
            IDbSet table;
            table = this.CreateDbSet(entity);
            return table;
        }

        IDbSet CreateDbSet(IEntityMapping entity)
        {
            return typeof(DbSet<>)
                .MakeGenericType(entity.EntityType)
                .GetConstructor(new Type[] { typeof(InternalDbContext), typeof(IEntityMapping) })
                .FastInvoke(this, entity) as IDbSet;
        }

        public IDbSet<T> Set<T>()
        {
            var type = typeof(T);
            return GetDbSet(type) as IDbSet<T>;
        }

        public IRepository<T> CreateRepository<T>()
        {
            return Set<T>();
        }

        IDbSet IDbContext.Set(Type type)
        {
            Guard.NotNull(type, "type");
            return GetDbSet(type);
        }

        internal IDbSet GetDbSet(Type type)
        {
            IDbSet set;
            if (!dbSetTable.TryGetValue(type, out set))
            {
                set = this.GetDbSet(dbConfiguration.GetMapping(type));
                lock (dbSetTable)
                    dbSetTable[type] = set;
            }
            return set;
        }

        IQueryable<S> IQueryProvider.CreateQuery<S>(Expression expression)
        {
            return new Query<S>(this, expression);
        }

        IQueryable IQueryProvider.CreateQuery(Expression expression)
        {
            Type elementType = ReflectionHelper.GetElementType(expression.Type);
            try
            {
                return (IQueryable)Activator.CreateInstance(typeof(Query<>).MakeGenericType(elementType), new object[] { this, expression });
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        S IQueryProvider.Execute<S>(Expression expression)
        {
            return (S)this.Execute(expression);
        }

        object IQueryProvider.Execute(Expression expression)
        {
            return this.Execute(expression);
        }

        object Execute(Expression expression)
        {
            try
            {
                ExecuteContext.Items = new Dictionary<string, object>();
                ExecuteContext.Dialect = Dialect;
                ExecuteContext.DbContext = this;

                LambdaExpression lambda = expression as LambdaExpression;

                //if (lambda == null && expression.NodeType != ExpressionType.Constant/* && !(Dialect is AccessDialect)*/)
                //    return QueryCache.Default.Execute(expression);

                Expression plan = this.GetExecutionPlan(expression);

                if (lambda != null)
                {
                    LambdaExpression fn = Expression.Lambda(lambda.Type, plan, lambda.Parameters);
                    //DynamicLinqGenerator.GenerateMethod(fn);
#if DEBUG
                      return fn.Compile(DebugInfoGenerator.CreatePdbGenerator());
#else
                    return fn.Compile();
#endif
                }
                else
                {
                    Expression<Func<object>> efn = Expression.Lambda<Func<object>>(Expression.Convert(plan, typeof(object)));
                    //DynamicLinqGenerator.GenerateMethod(efn);

#if DEBUG
                       Func<object> fn = efn.Compile(DebugInfoGenerator.CreatePdbGenerator());
#else
                    Func<object> fn = efn.Compile();
#endif

                    return fn();
                }
            }
            finally
            {
                ExecuteContext.DbContext = null;
                ExecuteContext.Dialect = null;
                if (ExecuteContext.Items != null)
                    ExecuteContext.Items.Clear();
                ExecuteContext.Items = null;
            }
        }

        internal string GetSqlText(Expression expression)
        {
            Expression plan = GetExecutionPlan(expression);
            return string.Join("\n\n", SqlGatherer.Gather(plan));
        }

        internal string BuildSql(Expression expression)
        {
            var fmt = dbConfiguration.Option.SqlBuilder(Dialect, dbConfiguration.Option.FuncRegistry);
            fmt.Visit(expression);
            return fmt.ToString();
        }


        internal class SqlGatherer : DbExpressionVisitor
        {
            public List<string> commands = new List<string>();
            static readonly Type ExectorType = typeof(ExecutionService);

            public static string[] Gather(Expression expression)
            {
                var gatherer = new SqlGatherer();
                gatherer.Visit(expression);
                return gatherer.commands.ToArray();
            }

            protected override Expression VisitMethodCall(MethodCallExpression m)
            {
                if (ExectorType.IsAssignableFrom(m.Method.DeclaringType))
                {
                    commands.Add(((m.Arguments[0] as NewExpression).Arguments[0] as ConstantExpression).Value as string);
                    return m;
                }
                return base.VisitMethodCall(m);
            }


        }

        internal Expression GetExecutionPlan(Expression expression)
        {
            LambdaExpression lambda = expression as LambdaExpression;
            if (lambda != null)
                expression = lambda.Body;

            var translation = expression;
            translation = PartialEvaluator.Eval(expression, ExpressionHelper.CanBeEvaluatedLocally);
            translation = FunctionBinder.Bind(this, translation);
            //translation = PartialEvaluator.Eval(translation, ExpressionHelper.CanBeEvaluatedLocally);
            translation = QueryBinder.Bind(ExpressionBuilder, this, translation);


            translation = AggregateRewriter.Rewrite(Dialect, translation);
            translation = UnusedColumnRemover.Remove(translation);
            translation = RedundantColumnRemover.Remove(translation);
            translation = RedundantSubqueryRemover.Remove(translation);
            translation = RedundantJoinRemover.Remove(translation);

            var bound = RelationshipBinder.Bind(ExpressionBuilder, translation);
            if (bound != translation)
            {
                translation = bound;
                translation = RedundantColumnRemover.Remove(translation);
                translation = RedundantJoinRemover.Remove(translation);
            }
            translation = ComparisonRewriter.Rewrite(ExpressionBuilder, translation);

            var rewritten = RelationshipIncluder.Include(ExpressionBuilder, this, translation);
            if (rewritten != translation)
            {
                translation = rewritten;
                translation = UnusedColumnRemover.Remove(translation);
                translation = RedundantColumnRemover.Remove(translation);
                translation = RedundantSubqueryRemover.Remove(translation);
                translation = RedundantJoinRemover.Remove(translation);
            }

            rewritten = SingletonProjectionRewriter.Rewrite(this.ExpressionBuilder, translation);
            if (rewritten != translation)
            {
                translation = rewritten;
                translation = UnusedColumnRemover.Remove(translation);
                translation = RedundantColumnRemover.Remove(translation);
                translation = RedundantSubqueryRemover.Remove(translation);
                translation = RedundantJoinRemover.Remove(translation);
            }

            rewritten = ClientJoinedProjectionRewriter.Rewrite(this, translation);
            if (rewritten != translation)
            {
                translation = rewritten;
                translation = UnusedColumnRemover.Remove(translation);
                translation = RedundantColumnRemover.Remove(translation);
                translation = RedundantSubqueryRemover.Remove(translation);
                translation = RedundantJoinRemover.Remove(translation);
            }

            //
            translation = this.ExpressionBuilder.Translate(translation);

            var parameters = lambda != null ? lambda.Parameters : null;
            Expression provider = Find(expression, parameters, typeof(InternalDbContext));
            if (provider == null)
            {
                Expression rootQueryable = Find(expression, parameters, typeof(IQueryable));
                provider = Expression.Property(rootQueryable, typeof(IQueryable).GetProperty("Provider"));
            }

            return ExecutionBuilder.Build(this.Dialect, this, translation, provider);
        }


        static Expression Find(Expression expression, IList<ParameterExpression> parameters, Type type)
        {
            if (parameters != null)
            {
                Expression found = parameters.FirstOrDefault(p => type.IsAssignableFrom(p.Type));
                if (found != null)
                    return found;
            }
            return TypedSubtreeFinder.Find(expression, type);
        }
    }
}
