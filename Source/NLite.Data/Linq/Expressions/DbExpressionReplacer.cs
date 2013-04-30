﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Linq.Expressions;

namespace NLite.Data.Linq.Expressions
{
    /// <summary>
    /// Replaces references to one specific instance of an expression node with another node.
    /// Supports DbExpression nodes
    /// </summary>
    class DbExpressionReplacer : DbExpressionVisitor
    {
        Expression searchFor;
        Expression replaceWith;

        private DbExpressionReplacer(Expression searchFor, Expression replaceWith)
        {
            this.searchFor = searchFor;
            this.replaceWith = replaceWith;
        }

        public static Expression Replace(Expression expression, Expression searchFor, Expression replaceWith)
        {
            return new DbExpressionReplacer(searchFor, replaceWith).Visit(expression);
        }

        public static Expression ReplaceAll(Expression expression, Expression[] searchFor, Expression[] replaceWith)
        {
            for (int i = 0, n = searchFor.Length; i < n; i++)
            {
                expression = Replace(expression, searchFor[i], replaceWith[i]);
            }
            return expression;
        }

        public override Expression Visit(Expression exp)
        {
            if (exp == this.searchFor)
            {
                return this.replaceWith;
            }
            return base.Visit(exp);
        }
    }
}
