﻿
namespace NLite.Data.Dialect
{
    class ExcelDialect : AccessDialect
    {
        public override bool SupportDelete
        {
            get
            {
                return false;
            }
        }
        //public override string QuoteTableName(string name)
        //{
        //    return "["+name+"$]";
        //}


    }
}
