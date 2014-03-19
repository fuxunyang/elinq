﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NLite.Data.Exceptions
{
    /// <summary>
    /// Collect data of an <see cref="DatabaseException"/> to be converted.
    /// </summary>
    public class DbExceptionContextInfo
    {
        /// <summary>
        /// The <see cref="System.Data.Common.DbException"/> to be converted.
        /// </summary>
        public Exception SqlException { get; set; }

        /// <summary>
        /// An optional error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The SQL that generate the exception
        /// </summary>
        public string Sql { get; set; }

        /// <summary>
        /// Optional EntityName where available in the original exception context.
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// Optional EntityId where available in the original exception context.
        /// </summary>
        public object EntityId { get; set; }

        public object Entity { get; set; }
    }
}
