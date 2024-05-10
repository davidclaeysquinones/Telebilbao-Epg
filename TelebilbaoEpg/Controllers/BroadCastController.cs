using Microsoft.AspNetCore.Mvc;
using TelebilbaoEpg.Database.Models;
using TelebilbaoEpg.Database.Repository;

namespace TelebilbaoEpg.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BroadCastController : ControllerBase
    {
        private IBroadCastRepository _broadCastRepository;

        public BroadCastController(IBroadCastRepository broadCastRepository)
        {
            _broadCastRepository = broadCastRepository;
        }

        [HttpGet("today")]
        public List<BroadCast> GetToday()
        {
            var today = DateTime.Now.Date;
            return _broadCastRepository.GetBroadCasts(DateOnly.FromDateTime(today));
        }

        [HttpGet]
        public List<BroadCast> Get(DateOnly from, DateOnly to)
        {
            return _broadCastRepository.GetBroadCasts(from, to);
        }
    }
}
