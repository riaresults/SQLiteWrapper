using SQLiteWrapper.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SQLiteWrapper
{
    /// <summary>
    /// This class hides the database implementation details and provided common functionality that
    /// allows to perform SELECT, UPDATE, DELETE SQL operations
    /// </summary>
    public class SQLiteDatabase : IDisposable
    {
        #region Private Variables

        private static SQLiteDatabase _instance;
        private readonly string _databaseName;
        private readonly string _databasePath;
        private bool _isValid;
        private SQLiteConnection _connection;

        // Flag: Has Dispose already been called? 
        private bool disposed = false;

        #region Lock objects
        private static object _syncRoot = new Object();
        private static object _deleteDBObject = new Object();
        private static object _executeNonQueryObject = new Object();
        private static object _executeScalarObject = new Object();
        #endregion
        #endregion

        #region Constructor
        private SQLiteDatabase(string path, string name)
        {
            _databaseName = name;
            _databasePath = string.Format(CultureInfo.InvariantCulture, "{0}\\{1}", path, name);
        }

        #endregion

        #region Properties
        /// <summary>
        /// Gets a singleton instance of the SQLiteDatabase
        /// </summary>
        public static SQLiteDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new Exception("SQLite Database instance not created");
                }

                return _instance;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the DB connection is valid
        /// </summary>
        public bool IsValid
        {
            get { return _isValid; }
            private set { _isValid = value; }
        }

        /// <summary>
        /// Gets the DB Connection instance
        /// </summary>
        public SQLiteConnection Connection
        {
            get
            {
                if (_connection == null || _connection.State != ConnectionState.Open)
                {
                    _connection = new SQLiteConnection(string.Format(CultureInfo.InvariantCulture, "Data Source = {0}; Version = 3;",
                        _databasePath));

                    _connection.Open();
                }

                return _connection;
            }
        }

        #endregion

        #region public static methods
        /// <summary>
        ///  Creates the DB Singleton Instance and ensures that database is available
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <param name="dbManager">If this is null, only the connection will be created. use the IDatabaseVersionManager to
        /// send the create and alter scripts specific to your CWF </param>
        public static void Create(IDatabaseVersionManager dbManager, string path, string name)
        {
            try
            {
                if (_instance != null)
                {
                    throw new Exception("SQLite Database object already created");
                }

                lock (_syncRoot)
                {
                    if (_instance == null)
                    {
                        _instance = new SQLiteDatabase(path, name);

                        if (dbManager != null)
                        {
                            _instance.IsValid = dbManager.CreateDatabase(System.Reflection.Assembly.GetCallingAssembly().GetName().Version);

                            if (_instance.IsValid)
                            {
                                _instance.PerformShrink();
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        #endregion

        #region Public Methods
        /// <summary>
        /// Deletes the database file
        /// </summary>
        public bool DeleteDatabase()
        {
            if (_connection != null)
            {
                _connection.Close();
                _connection.Dispose();
                return true;
            }

            lock (_deleteDBObject)
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        ///    Executes a query and returns the result in a DataTable
        /// </summary>
        /// <param name="sql">The SQL statement to be executed</param>
        /// <returns>A DataTable containing the result set.</returns>
        public DataTable GetDataTable(string sql)
        {
            var dataTable = new DataTable();
            try
            {
                using (var mycommand = new SQLiteCommand(Connection))
                {
                    mycommand.CommandText = sql;
                    using (SQLiteDataReader reader = mycommand.ExecuteReader())
                    {
                        dataTable.Load(reader);
                    }
                }
                return dataTable;
            }
            catch 
            {
                dataTable.Dispose();
                throw;
            }
        }

        /// <summary>
        ///     Allows the programmer to interact with the database for purposes other than a query.
        /// </summary>
        /// <param name="sql">The SQL to be run.</param>
        /// <param name="transaction">A SQLiteTransaction instance</param>
        /// <returns>An Integer containing the number of rows affected.</returns>

        public int ExecuteNonQuery(string sql, SQLiteTransaction transaction)
        {

            int rowsUpdated = 0;
            bool isLocalTransaction = transaction == null;

            try
            {
                lock (_executeNonQueryObject)
                {

                    if (isLocalTransaction)
                    {
                        transaction = Connection.BeginTransaction(IsolationLevel.ReadCommitted);
                    }

                    var databaseCommand = new SQLiteCommand(sql, Connection, transaction);
                    rowsUpdated = databaseCommand.ExecuteNonQuery();
                    databaseCommand.Dispose();
                    if (isLocalTransaction)
                    {
                        transaction.Commit();
                    }
                }
            }
            catch 
            {
                if (isLocalTransaction)
                {
                    transaction.Rollback();
                }
                throw;
            }
            finally
            {

                if (isLocalTransaction)
                {
                    transaction.Dispose();

                }
            }

            return rowsUpdated;
        }

        /// <summary>
        ///     Allows the programmer to retrieve single items from the DB.
        /// </summary>
        /// <param name="sql">The query to run.</param>
        /// <returns>A string.</returns>
        public string ExecuteScalar(string sql)
        {
            try
            {
                lock (_executeScalarObject)
                {
                    using (var mycommand = new SQLiteCommand(Connection))
                    {
                        mycommand.CommandText = sql;
                        object value = mycommand.ExecuteScalar();
                        if (value != null)
                        {
                            return value.ToString();
                        }
                    }
                }
            }
            catch 
            {
                throw;
            }
            return string.Empty;
        }

        /// <summary>
        ///     Allows the programmer to easily update rows in the DB.
        /// </summary>
        /// <param name="tableName">The table to update.</param>
        /// <param name="data">A dictionary containing Column names and their new values.</param>
        /// <param name="where">The where clause for the update statement.</param>
        /// <param name="transaction">A SQLiteTransaction instance</param>
        /// <returns>A boolean true or false to signify success or failure.</returns>
        public bool Update(string tableName, Dictionary<string, string> data, string where, SQLiteTransaction transaction)
        {
            string vals = string.Empty;
            bool returnCode = true;
            if (data != null && data.Count > 0)
            {
                foreach (var val in data)
                {
                    vals += string.Format(CultureInfo.InvariantCulture, " {0} = '{1}',", val.Key, val.Value);
                }

                vals = vals.Substring(0, vals.Length - 1);
            }

            string sql = string.Format(CultureInfo.InvariantCulture, "update {0} set {1} where {2};", tableName, vals, where);
            try
            {

                ExecuteNonQuery(sql, transaction);
                return returnCode;
            }
            catch
            {
                throw;
            }

        }

        /// <summary>
        ///     Allows the programmer to easily delete rows from the DB.
        /// </summary>
        /// <param name="tableName">The table from which to delete.</param>
        /// <param name="where">The where clause for the delete.</param>
        /// <param name="transaction">A SQLiteTransaction instance</param>
        /// <returns>A boolean true or false to signify success or failure.</returns>

        public bool Delete(string tableName, string where, SQLiteTransaction transaction)
        {
            bool returnCode = true;
            string query = string.Format(CultureInfo.InvariantCulture, "delete from {0} where {1};", tableName, where);
            try
            {
                ExecuteNonQuery(query, transaction);
                return returnCode;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        ///     Allows the programmer to easily insert into the DB
        /// </summary>
        /// <param name="tableName">The table into which we insert the _data.</param>
        /// <param name="data">A dictionary containing the column names and _data for the insert.</param>
        /// <param name="transaction">A SQLiteTransaction instance</param>
        /// <returns>A boolean true or false to signify success or failure.</returns>
        public bool Insert(string tableName, Dictionary<string, string> data, SQLiteTransaction transaction)
        {

            string columns = string.Empty;
            string values = string.Empty;
            bool returnCode = true;

            if (data == null || data.Count == 0)
            {
                return true;
            }

            foreach (var val in data)
            {
                columns += string.Format(CultureInfo.InvariantCulture, " {0},", val.Key);
                values += string.Format(CultureInfo.InvariantCulture, " '{0}',", val.Value);
            }

            columns = columns.Substring(0, columns.Length - 1);
            values = values.Substring(0, values.Length - 1);
            string sql = string.Format(CultureInfo.InvariantCulture, "insert into {0}({1}) values({2});", tableName, columns, values);
            try
            {
                ExecuteNonQuery(sql, transaction);
            }
            catch
            {
                returnCode = false;
                throw;
            }
            return returnCode;
        }

        /// <summary>
        ///     Allows the programmer to Get the maximum value within a table for a specified column
        /// </summary>
        /// <param name="tableName">Name of the table</param>
        /// <param name="columnName">Name of the column</param>
        /// <returns>Maximum value in that column</returns>
        public string GetMaxValueFromTable(string tableName, string columnName)
        {
            try
            {
                string sqlQuery = "SELECT MAX({0}) from {1}";
                string result = ExecuteScalar(string.Format(CultureInfo.InvariantCulture, sqlQuery, columnName, tableName));

                return result;
            }
            catch 
            {
                return string.Empty;
            }
        }

        /// <summary>
        ///     Allows the user to easily clear all _data from a specific table.
        /// </summary>
        /// <param name="table">The name of the table to clear.</param>
        /// <param name="transaction">A SQLite transaction instance</param>
        /// <returns>A boolean true or false to signify success or failure.</returns>
        public bool ClearTable(string table, SQLiteTransaction transaction)
        {
            try
            {
                ExecuteNonQuery(string.Format(CultureInfo.InvariantCulture, "delete from {0};", table), transaction);
                return true;
            }
            catch 
            {
                throw;
            }
        }

        /// <summary>
        /// Creates a new table in the database
        /// </summary>
        /// <param name="tableName">The name of the table</param>
        /// <param name="columns">Column information</param>
        public void AddTable(string tableName, SQLiteTransaction transaction, params Column[] columns)
        {
            if (CheckTableExists(tableName))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Table {1} already exists.", tableName));
            }

            string[] sqlColumns = new string[columns.Length];
            int i = 0;

            foreach (Column col in columns)
            {
                sqlColumns[i++] = GetSQLForColumn(col);
            }

            string sqlStatement = string.Format(CultureInfo.InvariantCulture, "CREATE TABLE {0} ({1})", tableName, string.Join(", ", sqlColumns));
            ExecuteNonQuery(sqlStatement, transaction);
        }

        /// <summary>
        /// Drops the table with the given name
        /// </summary>
        /// <param name="table">Name of the table to be deleted</param>
        /// <param name="transaction">SQLite transaction instance</param>
        /// <returns>TRUE if the table was dropped</returns>
        public bool DropTable(string tableName, SQLiteTransaction transaction)
        {
            string sql = string.Format(CultureInfo.InvariantCulture, "DROP TABLE IF EXISTS {0};", tableName);
            ExecuteNonQuery(sql, transaction);

            bool tableExists = CheckTableExists(tableName);

            return !tableExists;
        }

        /// <summary>
        /// Checks if the given table name exists in the SQLiteDatabase
        /// </summary>
        /// <param name="table">Name of the table</param>
        /// <returns>TRUE - if the table exists</returns>
        public bool CheckTableExists(string table)
        {
            try
            {
                string sql = string.Format(CultureInfo.InvariantCulture, "SELECT COUNT(*) FROM {0}", table);
                ExecuteNonQuery(sql, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Adds a new column to an existing table
        /// </summary>
        /// <param name="table">The name of the table</param>
        /// <param name="column">Column information</param>
        /// <param name="transaction">SQLite Transaction instance</param>
        public void AddColumn(string table, Column column, SQLiteTransaction transaction)
        {
            if (!CheckTableExists(table))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Table {1} does not exist.", column, table));
            }

            if (CheckColumnExists(table, column.Name))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Column {0} is already part of table {1}", column, table));
            }

            string sqlColumn = GetSQLForColumn(column);

            string sqlStatement = string.Format(CultureInfo.InvariantCulture, "ALTER TABLE {0} ADD {1}", table, sqlColumn);
            ExecuteNonQuery(sqlStatement, transaction);
        }

        /// <summary>
        /// Checks if the given column is part of the table
        /// </summary>
        /// <param name="table">Name of the table</param>
        /// <param name="column">Name of the column</param>
        /// <returns>True if it exists</returns>
        public bool CheckColumnExists(string table, string column)
        {
            try
            {
                string sql = string.Format(CultureInfo.InvariantCulture, "SELECT {0} FROM {1}", column, table);
                ExecuteNonQuery(sql, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the number of records present in the given table
        /// </summary>
        /// <param name="tableName">Name of the table</param>
        /// <returns>Number of records</returns>
        public int GetNumberRecords(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentNullException("tableName");
            }

            string sql = string.Format(CultureInfo.InvariantCulture, "SELECT COUNT(*) FROM {0}", tableName);
            string result = ExecuteScalar(sql);
            int count = 0;
            int.TryParse(result, out count);
            return count;
        }

        /// <summary>
        /// This operation removes the empty space in the database file
        /// thus reducing the size of the file
        /// </summary>
        /// <returns></returns>
        public bool PerformShrink()
        {
            try
            {
                var databaseCommand = new SQLiteCommand("VACUUM", Connection);
                int rowsUpdated = databaseCommand.ExecuteNonQuery();
                databaseCommand.Dispose();
                return true;
            }
            catch
            {
                throw;
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Returns the SQL statement for the given column
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        private string GetSQLForColumn(Column column)
        {
            string sqlType = ToSqlType(column.ColumnType, column.Size);
            string sqlNull = column.AllowsNull ? "NULL" : "NOT NULL";
            string sqlDefault = "";
            string sqlIdentity = "";

            if (column.DefaultValue != null)
            {
                string sep = column.ColumnType == typeof(string) ? "'" : ""; // wrap in '...'
                sqlDefault = string.Format(CultureInfo.InvariantCulture, "DEFAULT {0}{1}{0}", sep, column.DefaultValue);
            }
            else if (column.ColumnType == typeof(bool)) // boolean must have default to 0
            {
                sqlDefault = "DEFAULT 0";
            }

            if (column.IsIdentity || column.IsPrimaryKeyWithIdentity)
            {
                sqlIdentity = "PRIMARY KEY AUTOINCREMENT";
            }

            return string.Join(" ", new string[] { column.Name, sqlType, sqlIdentity, sqlNull, sqlDefault });
        }

        /// <summary>
        /// Returns the corresponding SQL Type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        private string ToSqlType(Type type, int size)
        {
            if (type == typeof(string))
                return "TEXT";
            else if (type == typeof(int))
                return "INTEGER";
            else if (type == typeof(float) || type == typeof(double))
                return "NUMERIC";
            else if (type == typeof(bool))
                return "INTEGER";
            else if (type == typeof(DateTime))
                return "DATETIME";
            else
                throw new NotSupportedException("Type not supported");
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        ///  Do not make this method virtual. 
        // A derived class should not be able to override this method. 
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method. 
            // Therefore, you should call GC.SupressFinalize to 
            // take this object off the finalization queue 
            // and prevent finalization code for this object 
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose(bool disposing) executes in two distinct scenarios. 
        /// If disposing equals true, the method has been called directly 
        /// or indirectly by a user's code. Managed and unmanaged resources 
        /// can be disposed. 
        /// If disposing equals false, the method has been called by the 
        /// runtime from inside the finalizer and you should not reference 
        /// other objects. Only unmanaged resources can be disposed. 
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called. 
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed 
                // and unmanaged resources. 
                if (disposing)
                {
                    // Dispose managed resources.
                    if (_connection != null)
                    {
                        _connection.Dispose();
                    }
                }

                // Note disposing has been done.
                disposed = true;
            }
        }

        // Use C# destructor syntax for finalization code. 
        // This destructor will run only if the Dispose method 
        // does not get called. 
        // It gives your base class the opportunity to finalize. 
        // Do not provide destructors in types derived from this class.
        ~SQLiteDatabase()
        {
            // Do not re-create Dispose clean-up code here. 
            // Calling Dispose(false) is optimal in terms of 
            // readability and maintainability.
            Dispose(false);
        }
        #endregion
    }
}
