﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NLite.Data.Exceptions
{
    class SqlExceptionConverter : ISQLExceptionConverter
    {
        public Exception Convert(DbExceptionContextInfo exceptionInfo)
        {
            return exceptionInfo.SqlException;
        }
    }
}