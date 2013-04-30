﻿using NLite.Data.Common;

namespace NLite.Data
{
    class EmptyDbLog : ISqlLog
    {
        public void LogMessage(string message)
        {
        }

        public void LogCommand(string commandText, NamedParameter[] parameters, object[] paramValues)
        {
        }


        public void LogParameters(NamedParameter[] parameters, object[] paramValues)
        {
        }
    }
}
