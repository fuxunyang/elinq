﻿using System;

namespace NLite.Data.Schema.Script.Executor
{
    class SqlServerScriptExecutor : NonFileDatabaseScriptExecutor
    {
        public override void CreateDatabase(DbConfiguration dbConfiguration, DatabaseScriptEntry script)
        {
            var connectionStringBuilder = dbConfiguration.DbProviderFactory.CreateConnectionStringBuilder();
            connectionStringBuilder.ConnectionString = dbConfiguration.ConnectionString;

            var dbName = dbConfiguration.DatabaseName;
            var createDatabase = "CREATE DATABASE " + dbName;
            dbConfiguration.sqlLogger().LogMessage(createDatabase);
            connectionStringBuilder["Database"] = "master";

            var log = dbConfiguration.sqlLogger();
            using (var ctx = dbConfiguration.CreateDbContext())
            {
                var conn = ctx.Connection;
                conn.ConnectionString = connectionStringBuilder.ConnectionString;

                var cmd = conn.CreateCommand();
                cmd.CommandText = createDatabase;
                conn.Open();
                cmd.ExecuteNonQuery();
                conn.ChangeDatabase(dbName);
                try
                {
                    ctx.UsingTransaction(() => CreateTables(log, script, ctx));
                }
                catch
                {
                    conn.ChangeDatabase("master");
                    cmd.CommandText = string.Format(DropDatabaseScriptTemplate, dbName);
                    cmd.ExecuteNonQuery();
                    throw;
                }
                finally
                {
                    conn.Close();
                }
            }
        }

        public override void DeleteDatabase(DbConfiguration dbConfiguration)
        {

            var connectionStringBuilder = dbConfiguration.DbProviderFactory.CreateConnectionStringBuilder();
            connectionStringBuilder.ConnectionString = dbConfiguration.ConnectionString;

            var dbName = dbConfiguration.DatabaseName;
            var dropDatabase = "DROP DATABASE " + dbConfiguration.Dialect.Quote(dbName);

            dropDatabase = string.Format(DropDatabaseScriptTemplate, dbName);

            dbConfiguration.sqlLogger().LogMessage(dropDatabase);

            using (var ctx = dbConfiguration.CreateDbContext())
            using (var cmd = ctx.Connection.CreateCommand())
            {
                connectionStringBuilder["Database"] = "master";
                ctx.Connection.ConnectionString = connectionStringBuilder.ConnectionString;

                cmd.CommandText = dropDatabase;
                ctx.Connection.Open();
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                }

                try
                {
                    ctx.Connection.ChangeDatabase(dbName);
                    throw new ApplicationException("drop database failed.");
                }
                catch
                {
                }

            }
        }

        const string DropDatabaseScriptTemplate =
                       @"
                        declare @dbname sysname 
                        set @dbname='{0}'

                        declare @s nvarchar(1000) 
                        declare tb cursor local for 
                        select s='kill '+cast(spid as varchar) 
                        from master..sysprocesses 
                        where dbid=db_id(@dbname) 

                        open tb 
                        fetch next from tb into @s 
                        while @@fetch_status=0 
                        begin 
                        exec(@s) 
                        fetch next from tb into @s 
                        end 
                        close tb 
                        deallocate tb 
                        exec('drop database ['+@dbname+']')  
                        ";

    }
}
