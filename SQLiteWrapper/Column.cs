using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SQLiteWrapper
{
    /// <summary>
    /// Represent a column within a table
    /// </summary>
    public struct Column
    {
        public string Name;
        public Type ColumnType;
        public int Size;
        public bool AllowsNull;
        public bool IsIdentity;
        public bool IsPrimaryKeyWithIdentity;
        public object DefaultValue;

        public Column(string name, Type type, int size, bool allowsNull, bool isIdentity, bool isPrimaryKeyWithIdentity, object defaultValue)
        {
            Name = name;
            ColumnType = type;
            Size = size;
            AllowsNull = allowsNull;
            IsIdentity = isIdentity;
            IsPrimaryKeyWithIdentity = isPrimaryKeyWithIdentity;
            DefaultValue = defaultValue;
        }
    }
}
