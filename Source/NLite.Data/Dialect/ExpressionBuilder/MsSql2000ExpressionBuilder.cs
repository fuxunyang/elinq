﻿using System.Linq.Expressions;
using NLite.Data.Linq.Expressions;
using NLite.Data.Mapping;

namespace NLite.Data.Dialect.ExpressionBuilder
{
    class MsSqlExpressionBuilder : DbExpressionBuilder
    {

        public override Expression GetGeneratedIdExpression(IMemberMapping member)
        {
            return new FunctionExpression(member.MemberType, "SCOPE_IDENTITY()", null);
        }
    }

    class MsSql2000ExpressionBuilder : MsSqlExpressionBuilder
    {
        public override Expression Translate(Expression expression)
        {
            // fix up any order-by's
            expression = OrderByRewriter.Rewrite(expression);
            expression = base.Translate(expression);
            expression = CrossJoinIsolator.Isolate(expression);
            expression = ThreeTopPagerRewriter.Rewrite(expression);
            expression = OrderByRewriter.Rewrite(expression);
            expression = UnusedColumnRemover.Remove(expression);
            expression = RedundantColumnRemover.Remove(expression);
            return expression;

        }
    }

    class MsSql2005ExpressionBuilder : MsSqlExpressionBuilder
    {
        public override System.Linq.Expressions.Expression Translate(Expression expression)
        {
            expression = OrderByRewriter.Rewrite(expression);

            expression = UnusedColumnRemover.Remove(expression);
            expression = RedundantColumnRemover.Remove(expression);
            expression = RedundantSubqueryRemover.Remove(expression);

            var rewritten = CrossApplyRewriter.Rewrite(expression);

            rewritten = CrossJoinRewriter.Rewrite(rewritten);

            if (rewritten != expression)
            {
                expression = rewritten;
                expression = UnusedColumnRemover.Remove(expression);
                expression = RedundantSubqueryRemover.Remove(expression);
                expression = RedundantJoinRemover.Remove(expression);
                expression = RedundantColumnRemover.Remove(expression);
            }


            expression = SkipToRowNumberRewriter.Rewrite(expression);
            expression = OrderByRewriter.Rewrite(expression);
            return expression;
        }
    }
}
