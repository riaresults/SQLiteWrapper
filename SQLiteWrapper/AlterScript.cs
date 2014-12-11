using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SQLiteWrapper
{
    /// <summary>
    /// Holds an SQL Statement as string and the version for which it should be applied
    /// </summary>
    public struct AlterScript
    {
        public string SQLStatement;
        public System.Version Version;

        public AlterScript(string version, string sqlStatement)
        {
            Version = new System.Version(version);
            SQLStatement = sqlStatement;
        }
    }
}
