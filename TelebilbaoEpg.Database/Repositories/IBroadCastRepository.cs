using System;
using System.Collections.Generic;
using TelebilbaoEpg.Database.Models;

namespace TelebilbaoEpg.Database.Repository
{
    public interface IBroadCastRepository
    {
        List<BroadCast> GetBroadCasts(DateOnly day);

        List<BroadCast> GetBroadCasts(DateOnly from, DateOnly to);

        void Add(BroadCast broadCast);
    }
}
