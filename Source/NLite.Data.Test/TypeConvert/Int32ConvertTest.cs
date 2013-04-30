﻿using System;
using System.Linq;
using System.Linq.Expressions;
using NLite.Data.Test.Primitive.Model;
using NUnit.Framework;
using TestMethod = NUnit.Framework.TestAttribute;

namespace NLite.Data.Test.TypeConvert
{
    public class Int32ConvertTest : TestBase<NullableTypeInfo>
    {
        protected override string ConnectionStringName
        {
            get
            {
                return "NumericDB";
            }
        }

        public virtual void Execute(string fieldName, object value, Expression<Func<NullableTypeInfo, bool>> filter)
        {
            var identityFieldValue = "zxmlx";
            var expected = new NullableTypeInfo { String = identityFieldValue };
            var field = typeof(NullableTypeInfo).GetField(fieldName);
            Assert.IsNotNull(field);
            field
                .SetValue(expected, PrimitiveMapper.Map(value, field.FieldType));

            //if (base.Db.Dialect is NLite.Data.Dialect.SQLiteDialect)
            //{
            //    base.Db.Connection.BeginTransaction();
            //}
            Table.Delete(p => true);
            Table.Insert(expected);

            var actual = Table
                .Where(p => p.String == expected.String)
                .Where(filter)
                .Select(p => new { Id = p.Id, p.String })
                .FirstOrDefault();

            Assert.IsNotNull(actual);

            Table.Delete(p => true);
        }
        [TestMethod]
        public virtual void ToBoolean()
        {
            Execute("Int32", 0, p => Convert.ToBoolean(p.Int32.Value) == false);
#if !Access
            Execute("Int32", 1, p => Convert.ToBoolean(p.Int32.Value) == true);
#endif
#if SqlServer
            Execute("Int32", -1, p => Convert.ToBoolean(p.Int32.Value) == true);
            Execute("Int32", 2, p => Convert.ToBoolean(p.Int32.Value) == true);
#endif
        }
        [TestMethod]

        public virtual void ToChar()
        {
            Execute("Int32", 0, p => Convert.ToChar(p.Int32.Value) == 0);
            Execute("Int32", 1, p => Convert.ToChar(p.Int32.Value) == 1);
            Execute("Int32", 2, p => Convert.ToChar(p.Int32.Value) == 2);
        }
        [TestMethod]
        public virtual void ToByte()
        {
            Execute("Int32", 0, p => Convert.ToByte(p.Int32.Value) == 0);
            Execute("Int32", 1, p => Convert.ToByte(p.Int32.Value) == 1);
            Execute("Int32", 2, p => Convert.ToByte(p.Int32.Value) == 2);
        }
        [TestMethod]
        public virtual void ToSByte()
        {
            Execute("Int32", 0, p => Convert.ToSByte(p.Int32.Value) == 0);
            Execute("Int32", 1, p => Convert.ToSByte(p.Int32.Value) == 1);
            Execute("Int32", 2, p => Convert.ToSByte(p.Int32.Value) == 2);
        }
        [TestMethod]
        public virtual void ToInt16()
        {
            Execute("Int32", 0, p => Convert.ToInt16(p.Int32.Value) == 0);
            Execute("Int32", 1, p => Convert.ToInt16(p.Int32.Value) == 1);
            Execute("Int32", 2, p => Convert.ToInt16(p.Int32.Value) == 2);
        }
        [TestMethod]
        public virtual void ToUInt16()
        {
            Execute("Int32", 0, p => Convert.ToUInt16(p.Int32.Value) == 0);
            Execute("Int32", 1, p => Convert.ToUInt16(p.Int32.Value) == 1);
            Execute("Int32", 2, p => Convert.ToUInt16(p.Int32.Value) == 2);
        }
        [TestMethod]
        public virtual void ToUInt32()
        {
            Execute("Int32", 0, p => Convert.ToUInt32(p.Int32.Value) == 0);
            Execute("Int32", 1, p => Convert.ToUInt32(p.Int32.Value) == 1);
            Execute("Int32", 2, p => Convert.ToUInt32(p.Int32.Value) == 2);
        }
        [TestMethod]
        public virtual void ToInt64()
        {
            Execute("Int32", 0, p => Convert.ToInt64(p.Int32.Value) == 0);
            Execute("Int32", 1, p => Convert.ToInt64(p.Int32.Value) == 1);
            Execute("Int32", 2, p => Convert.ToInt64(p.Int32.Value) == 2);
        }
        [TestMethod]
        public virtual void ToUInt64()
        {
            Execute("Int32", 0, p => Convert.ToUInt64(p.Int32.Value) == 0);
            Execute("Int32", 1, p => Convert.ToUInt64(p.Int32.Value) == 1);
            Execute("Int32", 2, p => Convert.ToUInt64(p.Int32.Value) == 2);
        }
        [TestMethod]
        public virtual void ToSingle()
        {
            Execute("Int32", 0, p => Convert.ToSingle(p.Int32.Value) == 0);
            Execute("Int32", 1, p => Convert.ToSingle(p.Int32.Value) == 1);
            Execute("Int32", 2, p => Convert.ToSingle(p.Int32.Value) == 2);
        }
        [TestMethod]
        public virtual void ToDouble()
        {
            Execute("Int32", 0, p => Convert.ToDouble(p.Int32.Value) == 0);
            Execute("Int32", 1, p => Convert.ToDouble(p.Int32.Value) == 1);
            Execute("Int32", 2, p => Convert.ToDouble(p.Int32.Value) == 2);
        }
        [TestMethod]
        public virtual void ToDecimal()
        {
            Execute("Int32", 0, p => Convert.ToDecimal(p.Int32.Value) == 0);
            Execute("Int32", 1, p => Convert.ToDecimal(p.Int32.Value) == 1);
            Execute("Int32", 2, p => Convert.ToDecimal(p.Int32.Value) == 2);
        }
        [TestMethod]
        public virtual void ToSString()
        {
            Execute("Int32", 0, p => Convert.ToString(p.Int32.Value) == "0");
            Execute("Int32", 1, p => Convert.ToString(p.Int32.Value) == "1");
            Execute("Int32", 2, p => Convert.ToString(p.Int32.Value) == "2");
        }
        [TestMethod]
        public virtual void Parse()
        {
            Execute("Int32", 0, p => int.Parse("0") == 0);
        }
    }
}
