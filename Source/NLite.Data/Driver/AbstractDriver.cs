﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using NLite.Data.Common;
using NLite.Reflection;

namespace NLite.Data.Driver
{
    public class AbstractDriver : IDriver
    {
        public virtual char NamedPrefix { get { return '@'; } }
        public virtual bool AllowsMultipleOpenReaders { get { return true; } }

        public virtual void AddParameter(DbCommand command, NamedParameter parameter, object value)
        {
            IDbDataParameter p = command.CreateParameter();
            InitializeParameter(p, parameter, value);
            command.Parameters.Add(p);
        }

        protected virtual void InitializeParameter(IDbDataParameter p, NamedParameter parameter, object value)
        {
            p.ParameterName = parameter.Name;
            p.Value = value ?? DBNull.Value;
            var sqlType = parameter.SqlType;

            if (parameter.SqlType != null)
            {

                if (sqlType.Precision > 0)
                    p.Precision = sqlType.Precision;
                if (sqlType.Scale > 0)
                    p.Scale = sqlType.Scale;
                if (sqlType.Required)
                    (p as DbParameter).IsNullable = false;
                if (sqlType.Length > 0)
                    p.Size = sqlType.Length;
                else
                    InitializeParameterLengthWhenZero(p, value, sqlType);
            }
            ConvertDBTypeToNativeType(p, parameter.sqlType.DbType);
        }

        private static void InitializeParameterLengthWhenZero(IDbDataParameter p, object value, SqlType sqlType)
        {
            switch (sqlType.DbType)
            {
                case DBType.NChar:
                case DBType.NVarChar:
                    {
                        var str = value as string;
                        if (string.IsNullOrEmpty(str))
                            p.Size = 2;
                        else
                            p.Size = str.Length * 2;
                        break;
                    }
                case DBType.Char:
                case DBType.VarChar:
                    {
                        var str = value as string;
                        if (string.IsNullOrEmpty(str))
                            p.Size = 1;
                        else
                            p.Size = str.Length * 1;
                        break;
                    }
            }
        }

        protected virtual void ConvertDBTypeToNativeType(IDbDataParameter p, DBType dbType)
        {
            switch (dbType)
            {
                case DBType.Binary: p.DbType = System.Data.DbType.Binary; break;
                case DBType.Boolean: p.DbType = System.Data.DbType.Boolean; break;
                case DBType.Byte: p.DbType = System.Data.DbType.Byte; break;
                case DBType.Char: p.DbType = System.Data.DbType.AnsiStringFixedLength; break;
                case DBType.DateTime: p.DbType = System.Data.DbType.DateTime; break;
                case DBType.Decimal: p.DbType = System.Data.DbType.Decimal; break;
                case DBType.Double: p.DbType = System.Data.DbType.Double; break;
                case DBType.Guid: p.DbType = System.Data.DbType.Guid; break;
                case DBType.Image: p.DbType = System.Data.DbType.Binary; break;
                case DBType.Int16: p.DbType = System.Data.DbType.Int16; break;
                case DBType.Int32: p.DbType = System.Data.DbType.Int32; break;
                case DBType.Int64: p.DbType = System.Data.DbType.Int64; break;
                case DBType.Currency: p.DbType = System.Data.DbType.Currency; break;
                case DBType.NChar: p.DbType = System.Data.DbType.StringFixedLength; break;
                case DBType.NText: p.DbType = System.Data.DbType.String; break;
                case DBType.NVarChar: p.DbType = System.Data.DbType.String; break;
                case DBType.Single: p.DbType = System.Data.DbType.Single; break;
                case DBType.Text: p.DbType = System.Data.DbType.AnsiString; break;
                //case DBType.Timestamp: p.DbType = System.Data.DbType.Binary; break;
                case DBType.VarChar: p.DbType = System.Data.DbType.AnsiString; break;
            }
        }

        protected virtual void GetParameterValues(DbCommand command, object[] paramValues)
        {
            if (paramValues != null)
            {
                for (int i = 0, n = command.Parameters.Count; i < n; i++)
                {
                    if (command.Parameters[i].Direction != System.Data.ParameterDirection.Input)
                    {
                        object value = command.Parameters[i].Value;
                        if (value == DBNull.Value)
                            value = null;
                        paramValues[i] = value;
                    }
                }
            }
        }

        static string ParseSqlParameter(string sql, char namedPrefix)
        {
            return namedPrefix != '@'
                ? sql.Replace('@', namedPrefix)
                : sql;
        }

        public virtual DbCommand CreateCommand(DbConnection conn, string sql, object namedParameters)
        {
            Guard.NotNullOrEmpty(sql, "sql");
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (namedParameters != null)
                AddParameters(cmd, namedParameters);
            return cmd;
        }


        public virtual void AddParameters(DbCommand cmd, object namedParameters)
        {
            var type = namedParameters.GetType();
            var dic = namedParameters as IDictionary<string, object>;
            if (dic != null)
            {
                foreach (var key in dic.Keys)
                    AddParameter(cmd, key, dic[key]);
                return;
            }

            var nvc = namedParameters as NameValueCollection;
            if (nvc != null)
            {
                foreach (string key in nvc.Keys)
                    AddParameter(cmd, key, nvc[key]);
                return;
            }

            var hs = namedParameters as Hashtable;
            if (hs != null)
            {
                foreach (string key in hs.Keys.OfType<string>())
                    AddParameter(cmd, key, hs[key]);
                return;
            }



            const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance;
            var items = from m in type
                       .GetFields(bindingFlags)
                       .Where(p => !p.HasAttribute<IgnoreAttribute>(true))
                       .Where(p => !p.Name.Contains("k__BackingField"))
                       .Cast<MemberInfo>()
                       .Union(type
                           .GetProperties(bindingFlags)
                           .Where(p => p.CanRead)
                           .Where(p => !p.HasAttribute<IgnoreAttribute>(true))
                           .Cast<MemberInfo>()
                           ).Distinct()
                        select m;

            foreach (var item in items.ToArray())
                AddParameter(cmd, item.Name, item.GetGetter()(namedParameters));
        }

        protected virtual void AddParameter(DbCommand cmd, string name, object item)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            if (item == null)
                p.Value = DBNull.Value;
            else
            {
                var type = item.GetType();
                if (type.IsNullable())
                    item = Converter.Convert(item, Nullable.GetUnderlyingType(type));

                InitializeParameter(item, p, type);
            }
            cmd.Parameters.Add(p);
        }

        protected virtual void InitializeParameter(object item, DbParameter p, Type type)
        {
            var typeCode = Type.GetTypeCode(type);
            switch (typeCode)
            {
                case TypeCode.SByte:
                    item = Convert.ToByte(item);
                    break;
                case TypeCode.UInt16:
                    item = Convert.ToInt16(item);
                    break;
                case TypeCode.UInt32:
                    item = Convert.ToInt32(item);
                    break;
                case TypeCode.UInt64:
                    item = Convert.ToInt64(item);
                    break;
            }
            p.Value = item;
        }

    }
}
