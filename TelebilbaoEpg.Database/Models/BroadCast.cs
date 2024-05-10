using SQLite;
using System;
namespace TelebilbaoEpg.Database.Models
{
    public class BroadCast
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; }  = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;
    }
}
