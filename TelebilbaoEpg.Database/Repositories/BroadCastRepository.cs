using System;
using System.Collections.Generic;
using System.Linq;
using TelebilbaoEpg.Database.Models;

namespace TelebilbaoEpg.Database.Repository
{
    public class BroadCastRepository : BaseRepository, IBroadCastRepository
    {
        public void Add(BroadCast broadCast)
        {
            _db.Insert(broadCast);
        }

        public List<BroadCast> GetBroadCasts(DateOnly day)
        {
           return  _db.Table<BroadCast>()
                .ToList()
                .Where(b => DateOnly.FromDateTime(b.From.Date) == day || DateOnly.FromDateTime(b.To) == day)
                .OrderBy(b => b.From)
                .ToList();
        }

        public List<BroadCast> GetBroadCasts(DateOnly from, DateOnly to)
        {
            return _db.Table<BroadCast>()
              .ToList()
              .Where(b => (DateOnly.FromDateTime(b.From) >= from || DateOnly.FromDateTime(b.To) >= from) && (DateOnly.FromDateTime(b.From) <= to || DateOnly.FromDateTime(b.To) <= to))
              .OrderBy(b => b.From)
              .ToList();
        }
    }
}
