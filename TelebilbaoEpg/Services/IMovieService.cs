namespace Telebilbap_Epg.Services
{
    public interface IMovieService
    {
        Task<Movie?> GetMovie(string title, int? year);
    }
}
