using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SQLiteWrapper.Interfaces
{
    public interface IDatabaseVersionManager
    {
        /// <summary>
        /// SQLite Database Name
        /// </summary>
        string DatabaseName { get; }

        string DatabasePath { get; }

        /// <summary>
        /// Returns TRUE if the database file exists
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        bool CheckDatabaseExistance(string fileName);

        /// <summary>
        /// Creates a new database file with the name from DatabaseName and located at the
        /// path specified in DatabasePath
        /// </summary>
        /// <returns></returns>
        bool CreateDatabaseFile();

        /// <summary>
        /// Applies alter statements on the existing database
        /// </summary>
        /// <param name="callingAssemblyVersion"></param>
        /// <returns></returns>
        bool ApplySchemaUpdates(System.Version callingAssemblyVersion);

        /// <summary>
        /// Updates the database version
        /// </summary>
        /// <param name="callingAssemblyVersion"></param>
        void UpdateVersion(System.Version callingAssemblyVersion);

        /// <summary>
        /// Checks if the DB File exists, and applies alter scrips and version updates
        /// if the DB does not exist, it applies the create scrips
        /// </summary>
        /// <param name="callingAssemblyVersion"></param>
        /// <returns></returns>
        bool CreateDatabase(System.Version callingAssemblyVersion);
    }
}
