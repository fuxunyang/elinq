﻿
namespace NLite.Data.Dialect.Function.Access
{
    class AccessDecimalFunctions : IDecimalFunctions
    {
        public IFunctionView Remainder
        {
            get { return FunctionView.VarArgs("(", "MOD", ")"); }
        }
    }
}
