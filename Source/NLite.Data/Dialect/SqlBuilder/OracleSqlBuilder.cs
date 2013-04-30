﻿using System;
using System.Linq.Expressions;
using NLite.Data.Linq.Expressions;
using NLite.Reflection;

namespace NLite.Data.Dialect.SqlBuilder
{
    internal class OracleSqlBuilder : DbSqlBuilder
    {

        protected override void RegisterCastTypes()
        {
            RegisterCastType(DBType.VarChar, "VARCHAR(50)");
            RegisterCastType(DBType.Char, "VARCHAR(50)");
            RegisterCastType(DBType.NVarChar, "VARCHAR2(100)");
            RegisterCastType(DBType.NChar, "VARCHAR2(100)");
            RegisterCastType(DBType.Binary, "BLOB");
            RegisterCastType(DBType.Boolean, "NUMBER(1,0)");
            RegisterCastType(DBType.Byte, "NUMBER");
            RegisterCastType(DBType.Single, "NUMBER");
            RegisterCastType(DBType.Double, "NUMBER");
            RegisterCastType(DBType.Decimal, "NUMBER");
            RegisterCastType(DBType.Int16, "NUMBER");
            RegisterCastType(DBType.Int32, "NUMBER");
            RegisterCastType(DBType.Int64, "NUMBER");
            RegisterCastType(DBType.Guid, "RAW(16)");
            RegisterCastType(DBType.DateTime, "DATE");
        }

        protected override void AppendParameterName(string name)
        {
            this.Append(":");
            this.Append(name);
        }

        protected override void AppendAsAliasName(string aliasName)
        {
            this.Append(aliasName);
        }

        protected override void AppendAsColumnName(string columnName)
        {
            this.Append(columnName.ToUpper());
        }

        protected override void AppendTableName(string tableName)
        {
            base.AppendTableName(tableName.ToUpper());
        }

        public override Expression Visit(Expression exp)
        {
            base.Visit(exp);
            var p = exp as SelectExpression;
            if (p != null && p.From == null)
            {
                sb.AppendLine();
                sb.Append("FROM SYS.DUAL");
            }
            return exp;
        }

        protected override Expression VisitValue(Expression expr)
        {
            if (IsPredicate(expr))
            {
                this.Append("CASE WHEN (");
                this.Visit(expr);
                this.Append(") THEN 1 ELSE 0 END");
                return expr;
            }
            return base.VisitValue(expr);
        }
        protected override void WriteTopClause(Expression expression)
        {
        }
        protected override Expression VisitSelect(SelectExpression select)
        {
            if (select.Take != null)
            {
                this.Append("SELECT * FROM (");
            }
            Expression exp = base.VisitSelect(select);
            if (select.Take != null)
            {
                this.Append(") WHERE ROWNUM<=");
                this.Append(select.Take);
            }
            return exp;
        }

        protected override Expression VisitRowNumber(RowNumberExpression rowNumber)
        {
            this.Append("ROW_NUMBER() OVER(");
            if (rowNumber.OrderBy != null && rowNumber.OrderBy.Count > 0)
            {
                this.Append("ORDER BY ");
                for (int i = 0, n = rowNumber.OrderBy.Count; i < n; i++)
                {
                    OrderExpression exp = rowNumber.OrderBy[i];
                    if (i > 0)
                        this.Append(", ");
                    this.VisitValue(exp.Expression);
                    if (exp.OrderType != OrderType.Ascending)
                        this.Append(" DESC");
                }
            }
            this.Append(")");
            return rowNumber;
        }

        protected override Expression VisitIf(IFCommand ifx)
        {
            if (!this.Dialect.SupportMultipleCommands)
            {
                return base.VisitIf(ifx);
            }
            this.Append("IF ");
            this.Visit(ifx.Check);
            this.AppendLine(Indentation.Same);
            this.Append("THEN BEGIN");
            this.AppendLine(Indentation.Inner);
            this.VisitStatement(ifx.IfTrue);
            this.AppendLine(Indentation.Outer);
            if (ifx.IfFalse != null)
            {
                this.Append("ELSE BEGIN");
                this.AppendLine(Indentation.Inner);
                this.VisitStatement(ifx.IfFalse);
                this.AppendLine(Indentation.Outer);
            }
            this.Append("END IF;");
            return ifx;
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            switch (b.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    if (this.IsBoolean(b.Left.Type))
                    {
                        return base.VisitBinary(b);
                    }
                    else
                    {
                        this.Append("BITAND(");
                        this.VisitValue(b.Left);
                        this.Append(",");
                        this.VisitValue(b.Right);
                        this.Append(")");
                        break;
                    }
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    if (this.IsBoolean(b.Left.Type))
                    {
                        return base.VisitBinary(b);
                    }
                    else
                    {
                        //OR = x-bitand(x,y)+y
                        this.Append("(");
                        this.VisitValue(b.Left);
                        this.Append("-BITAND(");
                        this.VisitValue(b.Left);
                        this.Append(",");
                        this.VisitValue(b.Right);
                        this.Append(")+");
                        this.VisitValue(b.Right);
                        this.Append(")");
                        break;
                    }
                case ExpressionType.ExclusiveOr:
                    //XOR: x-2*bitand(x,y)+y
                    this.Append("(");
                    this.VisitValue(b.Left);
                    this.Append("-2*BITAND(");
                    this.VisitValue(b.Left);
                    this.Append(",");
                    this.VisitValue(b.Right);
                    this.Append(")+");
                    this.VisitValue(b.Right);
                    this.Append(")");
                    break;
                case ExpressionType.Modulo:
                    this.Append("MOD(");
                    this.VisitValue(b.Left);
                    this.Append(",");
                    this.VisitValue(b.Right);
                    this.Append(")");
                    break;
                case ExpressionType.LeftShift:
                    this.Append("(");
                    this.VisitValue(b.Left);
                    this.Append("*POWER(2,");
                    this.VisitValue(b.Right);
                    this.Append("))");
                    break;
                case ExpressionType.RightShift:
                    this.Append("(");
                    this.VisitValue(b.Left);
                    this.Append("/POWER(2,");
                    this.VisitValue(b.Right);
                    this.Append("))");
                    break;
                case ExpressionType.Coalesce:
                    this.Append("COALESCE(");
                    this.VisitValue(b.Left);
                    this.Append(", ");
                    Expression right = b.Right;
                    while (right.NodeType == ExpressionType.Coalesce)
                    {
                        BinaryExpression rb = (BinaryExpression)right;
                        this.VisitValue(rb.Left);
                        this.Append(", ");
                        right = rb.Right;
                    }
                    this.VisitValue(right);
                    this.Append(")");
                    break;
                case ExpressionType.Power:
                    Append("POWER(");
                    this.VisitValue(b.Left);
                    Append(", ");
                    this.VisitValue(b.Right);
                    Append(")");
                    break;
                case ExpressionType.Divide:
                    if (isInteger(b.Left.Type) && isInteger(b.Right.Type))
                    {
                        this.Append("TRUNC(");
                        base.VisitBinary(b);
                        this.Append(")");
                    }
                    else
                    {
                        base.VisitBinary(b);
                    }
                    break;
                default:
                    return base.VisitBinary(b);
            }
            return b;
        }

        static Func<Type, bool> isInteger = type =>
        {
            Type nnType = type.IsNullable() ? Nullable.GetUnderlyingType(type) : type;
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        };
        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    string op = this.GetOperator(u);
                    if (IsBoolean(u.Operand.Type) || op.Length > 1)
                    {
                        return base.VisitUnary(u);
                    }
                    else
                    {
                        //NOT: -1-x
                        this.Append("(-1-");
                        this.VisitValue(u.Operand);
                        this.Append(")");
                    }
                    break;
                default:
                    return base.VisitUnary(u);
            }
            return u;
        }
        protected override Expression VisitConditional(ConditionalExpression c)
        {
            if (this.IsPredicate(c.Test))
            {
                this.Append("(CASE WHEN ");
                this.VisitPredicate(c.Test);
                this.Append(" THEN ");
                this.VisitValue(c.IfTrue);
                Expression ifFalse = c.IfFalse;
                while (ifFalse != null && ifFalse.NodeType == ExpressionType.Conditional)
                {
                    ConditionalExpression fc = (ConditionalExpression)ifFalse;
                    this.Append(" WHEN ");
                    this.VisitPredicate(fc.Test);
                    this.Append(" THEN ");
                    this.VisitValue(fc.IfTrue);
                    ifFalse = fc.IfFalse;
                }
                if (ifFalse != null)
                {
                    this.Append(" ELSE ");
                    this.VisitValue(ifFalse);
                }
                this.Append(" END)");
            }
            else
            {
                this.Append("(CASE ");
                this.VisitValue(c.Test);
                this.Append(" WHEN 0 THEN ");
                this.VisitValue(c.IfFalse);
                this.Append(" ELSE ");
                this.VisitValue(c.IfTrue);
                this.Append(" END)");
            }
            return c;
        }

        protected override Expression VisitAggregate(AggregateExpression aggregate)
        {
            switch (aggregate.AggregateName)
            {
                case "Average":
                    this.WriteTruncMaxDecimalDigitsStart();
                    base.VisitAggregate(aggregate);
                    this.WriteTruncMaxDecimalDigitsEnd();
                    break;
                default:
                    return base.VisitAggregate(aggregate);
            }
            return aggregate;
        }

        //these two functions are to get around OracleClient issue: OCI-22053: overflow error
        void WriteTruncMaxDecimalDigitsStart()
        {
            this.Append("TRUNC(");
        }
        void WriteTruncMaxDecimalDigitsEnd()
        {
            const int MaxDecimalDigits = 20;
            this.Append(",");
            this.Append(MaxDecimalDigits);
            this.Append(")");
        }
        protected override void BuildConverterFunction(Expression from, Type fromType, Type toType)
        {
            const string dateFormat = ",'yyyy-mm-dd HH24:mi:ss')";
            if (toType == Types.Boolean)
            {
                if (IsInteger(fromType))
                {
                    Visit(from);
                    return;
                }
            }
            if (toType == Types.DateTime)
            {
                sb.Append("TO_DATE(");
                Visit(from);
                sb.Append(dateFormat);
                return;
            }
            if (toType == Types.String)
            {
                if (fromType == Types.DateTime)
                {
                    sb.Append("TO_CHAR(");
                    Visit(from);
                    sb.Append(dateFormat);
                    return;
                }
                if (IsInteger(fromType))
                {
                    sb.Append("TO_NUMBER(");
                    Visit(from);
                    sb.Append(")");
                    return;
                }
            }
            base.BuildConverterFunction(from, fromType, toType);
        }
    }
}
