﻿
namespace NLite.Data.Dialect.Function.SqlCe
{
    class SqlCEDecimalFunctions : IDecimalFunctions
    {
        public IFunctionView Remainder
        {
            get { return FunctionView.VarArgs("(", "%", ")"); }
        }
    }
}
