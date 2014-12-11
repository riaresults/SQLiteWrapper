using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace SQLiteWrapper.Interfaces
{
    /// <summary>
    /// Defines a set of scrips to be a applied on a SQLite DB
    /// </summary>
    public interface IDatabaseScripts
    {
        /// <summary>
        /// List of alter scripts to be run on the SQLite DB
        /// </summary>
        List<AlterScript> AlterScriptList { get; set; }

        /// <summary>
        /// Runs a series of scripts meant to create the SQLite tables
        /// </summary>
        /// <param name="dbConnection"></param>
        /// <param name="transaction"></param>
        void RunCreateScripts(SQLiteConnection dbConnection, SQLiteTransaction transaction);

        /// <summary>
        /// Applies the alter scripts on the SQLite database based on the version
        /// </summary>
        /// <param name="systemVersion"></param>
        /// <param name="dbConnection"></param>
        /// <param name="transaction"></param>
        void RunAlterScripts(System.Version systemVersion, SQLiteConnection dbConnection, SQLiteTransaction transaction);
    }
}
