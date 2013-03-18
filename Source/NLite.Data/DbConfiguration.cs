﻿
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NLite.Data.Dialect;
using NLite.Data.Driver;
using NLite.Data.Mapping;
using NLite.Data.Schema;


namespace NLite.Data
{
    /// <summary>
    /// 数据库配置类，该类是整个框架的入口点
    /// </summary>
    [DebuggerDisplay("Name='{Name}',DbProviderName='{DbProviderName}',ConnectionStringName='{ConnectionStringName}',ConnectionString='{ConnectionString}'")]
    public partial class DbConfiguration
    {
        /// <summary>
        /// DbProvider 名称列表
        /// </summary>
        public static string[] ProviderNames
        {
            get
            {
                return providerNames.ToArray();
            }
        }
        /// <summary>
        /// DbConfiguration字典
        /// </summary>
        public static IDictionary<string, DbConfiguration> Items
        {
            get
            {
                return items;
            }
        }

        /// <summary>
        /// 得到或设置缺省的DbConfiguration
        /// </summary>
        internal static DbConfiguration DefaultDbConfiguration;

        /// <summary>
        /// 得到缺省的DbConfiguration
        /// </summary>
        public static DbConfiguration Default
        {
            get { return DefaultDbConfiguration; }
        }

        /// <summary>
        /// 指示当前DbConfiguration是否是缺省的DbConfiguration
        /// </summary>
        public bool IsDefault
        {
            get { return DefaultDbConfiguration == this; }
        }

        /// <summary>
        /// 把当前DbConfiguration标志为缺省DbConfiguration
        /// </summary>
        /// <returns></returns>
        public DbConfiguration MakeDefault()
        {
            DefaultDbConfiguration = this;
            return this;
        }

        /// <summary>
        /// DbProvider 名称
        /// </summary>
        public string DbProviderName { get; private set; }
        /// <summary>
        /// DbConfiguration 名称，用来唯一标识一个DbConfiguration实例
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// 连接字符串
        /// </summary>
        public string ConnectionString { get; private set; }
        /// <summary>
        /// DbProvider 工厂
        /// </summary>
        public DbProviderFactory DbProviderFactory { get; private set; }

        /// <summary>
        /// 数据库驱动
        /// </summary>
        public IDriver Driver { get { return this.Option.Driver; } }


        /// <summary>
        /// 数据库方言
        /// </summary>
        public IDialect Dialect { get { return this.Option.Dialect; } }

        internal long totalOpenConnection;

        /// <summary>
        /// 创建DbContext（必须先注册实体到数据表的映射后才可创建DbContext）
        /// </summary>
        /// <returns></returns>
        public IDbContext CreateDbContext()
        {
            Guard.NotNull(Option.Dialect, "dialect");

            IDbContext ctx = null;
            if (connection == null)
                ctx = new InternalDbContext(this);
            else
                ctx = CreateDbContext(ctx);
            return ctx;
        }

        private IDbContext CreateDbContext(IDbContext ctx)
        {
            connection.StateChange += new StateChangeEventHandler(connection_StateChange);
            if (!(connection is DbConnectionWrapper))
            {
                switch (DbProviderName)
                {
                    case DbProviderNames.Oracle_ODP:
                        connection = new ODPConnectionWrapper(this,connection);
                        break;
                    case DbProviderNames.SQLite:
                        connection = new SQLiteConnectionWrapper(this, connection) { IsFileDatabase = true };
                        break;
                    case DbProviderNames.SqlCe40:
                    case DbProviderNames.SqlCe35:
                    case DbProviderNames.Oledb :
                        connection = new DbConnectionWrapper(this, connection) { IsFileDatabase = true };
                        break;
                    default:
                        connection = new DbConnectionWrapper(this,connection);
                        break;

                }
            }

            ctx = new InternalDbContext(this, connection);
            return ctx;
        }

        /// <summary>
        /// 创建DbConnection对象
        /// </summary>
        /// <returns></returns>
        public DbConnection CreateDbConnection()
        {
            return connectionCreator();
        }

        void connection_StateChange(object sender, StateChangeEventArgs e)
        {
            if (e.CurrentState == ConnectionState.Closed)
            {
                connection.StateChange -= connection_StateChange;
                connection = null;
            }
        }

        private Func<DbConnection> connectionCreator;
      
         /// <summary>
        /// 创建数据库
        /// </summary>
        public void CreateDatabase()
        {
            var dbName = DatabaseName;
            var scriptGenerator = Option.ScriptGenerator();
            var script = scriptGenerator.Build(Dialect, mappings.Values.Cast<IEntityMapping>().ToArray(), dbName);

            var scriptExecutor = Option.ScriptExecutor();
            scriptExecutor.CreateDatabase(this, script);
        }

        private string databaseName;

        /// <summary>
        /// 数据库名称
        /// </summary>
        public string DatabaseName
        {
            get
            {
                if (databaseName.HasValue()) return databaseName;
                databaseName = GetDatabaseName();
                return databaseName;
            }
        }

        string GetDatabaseName()
        {
            var connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = this.ConnectionString };
            object objDbName;
            string dbName = null;

            if (DbProviderName == DbProviderNames.Oracle || DbProviderName == DbProviderNames.Oracle_ODP)
            {
                if (connectionStringBuilder.TryGetValue("USER ID", out objDbName))
                    dbName = objDbName.ToString();
                else if (connectionStringBuilder.TryGetValue("UID", out objDbName))
                    dbName = objDbName.ToString();
            }
            else
            {
                if (connectionStringBuilder.TryGetValue("Initial Catalog", out objDbName))
                    dbName = objDbName.ToString();
                else if (connectionStringBuilder.TryGetValue("Database", out objDbName))
                    dbName = objDbName.ToString();
                else if (connectionStringBuilder.TryGetValue("AttachDBFileName", out objDbName))
                    dbName = objDbName.ToString();
                else if (connectionStringBuilder.TryGetValue("Data Source", out objDbName))
                    dbName = objDbName.ToString();

                var dataDirectory = AppDomain.CurrentDomain.GetData("DataDirectory") as string;

                if (dbName == null) return null;

                if (dbName.Contains(@"|DataDirectory|\"))
                {
                    if (dataDirectory == null)
                        dataDirectory = CheckDataDirectory(dataDirectory);
                    dbName = dbName.Replace(@"|DataDirectory|\", dataDirectory + @"\");
                }
                else if (dbName.Contains(@"|DataDirectory|"))
                {
                    if (dataDirectory == null)
                        dataDirectory = CheckDataDirectory(dataDirectory);
                    dbName = dbName.Replace(@"|DataDirectory|", dataDirectory + @"\");
                }
            }
            return dbName;
        }

        private static string CheckDataDirectory(string dataDirectory)
        {
            dataDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (dataDirectory.IndexOf("\\bin\\") > 0)
            {
                if (dataDirectory.EndsWith("\\bin\\Debug"))
                    dataDirectory = dataDirectory.Replace("\\bin\\Debug", "\\");
                if (dataDirectory.EndsWith("\\bin\\Release"))
                    dataDirectory = dataDirectory.Replace("\\bin\\Release", "\\");
            }
            if (!dataDirectory.EndsWith("App_Data\\"))
                dataDirectory = dataDirectory + "App_Data\\";
            if (!Directory.Exists(dataDirectory))
                Directory.CreateDirectory(dataDirectory);

            AppDomain.CurrentDomain.SetData("DataDirectory", dataDirectory);
            return dataDirectory;
        }

         /// <summary>
        /// 创建数据表
        /// </summary>
        public void CreateTables()
        {
            var scriptGenerator = Option.ScriptGenerator();
            var script = scriptGenerator.Build(Dialect, mappings.Values.Cast<IEntityMapping>().ToArray(),  null);

            var scriptExecutor = Option.ScriptExecutor();
            scriptExecutor.CreateTables(this, script);
        }

        /// <summary>
        /// 判断数据库是否存在
        /// </summary>
        public bool DatabaseExists()
        {
            var scriptExecutor = Option.ScriptExecutor();
            return scriptExecutor.DatabaseExists(this);
        }

         /// <summary>
        /// 删除数据库
        /// </summary>
        public void DeleteDatabase()
        {
            var scriptExecutor = Option.ScriptExecutor();
            scriptExecutor.DeleteDatabase(this);
        }

        private IDatabaseSchema schema;
        /// <summary>
        /// 数据库Schema
        /// </summary>
        public IDatabaseSchema Schema
        {
            get
            {
                if (schema != null)
                    return schema;
                if (Option.SchemaLoader == null)
                    throw new NotImplementedException();

                var schemaLoader = Option.SchemaLoader();

                lock (this)
                    schema = schemaLoader.Load(this);
                return schema;
            }
        }
    
        internal Func<ISqlLog> sqlLogger = () => SqlLog.Debug;
        /// <summary>
        /// 设置sql语句输出日志
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        public DbConfiguration SetSqlLogger(Func<ISqlLog> logger)
        {
            Guard.NotNull(logger, "logger");
            this.sqlLogger = logger;
            return this;
        }

        private DbConfiguration(
            string providerName
            , string name
            , string connectionString
            , DbProviderFactory dbProviderFactory)
        {
            DbProviderName = providerName;
            Name = name;
            DbProviderFactory = dbProviderFactory;
            ConnectionString = connectionString;
            SetMappingConversion(MappingConversion.Default);

            switch (providerName)
            {
                case DbProviderNames.SQLite:
                    connectionCreator = () => new SQLiteConnectionWrapper(this,DbProviderFactory.CreateConnection()) { ConnectionString = connectionString, IsFileDatabase = true };
                    break;
                case DbProviderNames.Oledb:
                case DbProviderNames.SqlCe35:
                case DbProviderNames.SqlCe40:
                    connectionCreator = () => new DbConnectionWrapper(this,DbProviderFactory.CreateConnection()) { ConnectionString = connectionString, IsFileDatabase = true };
                    break;
                case DbProviderNames.SqlServer:
                    var builder = DbProviderFactory.CreateConnectionStringBuilder();
                    builder.ConnectionString = connectionString;
                    builder["MultipleActiveResultSets"] = true;
                    connectionCreator = () => new DbConnectionWrapper(this,DbProviderFactory.CreateConnection()) { ConnectionString = connectionString };
                    break;
                case DbProviderNames.Oracle_ODP:
                     connectionCreator = () => new ODPConnectionWrapper(this,DbProviderFactory.CreateConnection()) { ConnectionString = connectionString };
                    break;
                default:
                     connectionCreator = () => new DbConnectionWrapper(this,DbProviderFactory.CreateConnection()) { ConnectionString = connectionString };
                    break;
            }

            if (ConfigurationManager.ConnectionStrings.Count == 1)
                MakeDefault();
        }
    }
}
