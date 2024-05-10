using SQLite;
using System.IO;
using TelebilbaoEpg.Database.Models;

namespace TelebilbaoEpg.Database.Repository
{
    public abstract class BaseRepository
    {
        protected SQLiteConnection _db;

        public BaseRepository()
        {
            var storeFile = "/data/telebilbaoEpg.db";

#if DEBUG
            storeFile = storeFile.Replace("/data/", "");
#endif

            // Get an absolute path to the database file
            var databasePath = Path.Combine(Directory.GetCurrentDirectory(), storeFile);

            _db = new SQLiteConnection(databasePath);
            _db.CreateTable<BroadCast>();
        }
    }
}
