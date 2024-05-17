using System.Text.Json.Serialization;

namespace Telebilbap_Epg.Services
{
    public class MovieService : IMovieService
    {
        private HttpClient _httpClient;

        private IConfiguration _configuration;

        public MovieService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<Movie?> GetMovie(string title, int? year)
        {
            Movie? ret = null;

            var apiUrl = _configuration.GetValue<string>("MovieApi:Url");
            var apiKey = _configuration.GetValue<string>("MovieApi:ApiKey");
            var imageUrl = _configuration.GetValue<string>("MovieApi:ImageUrl");

            var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);

            queryString.Add("query", title);
            queryString.Add("language", "es");

            if (year.HasValue)
            {
                queryString.Add("year", year.ToString());
            }

            queryString.Add("api_key", apiKey);

            var url = $"{apiUrl}/3/search/movie?{queryString}";

            var requestResponse = await _httpClient.GetAsync(url);

            var results = await requestResponse.Content.ReadFromJsonAsync<ApiResults>();

            if(results != null && results.TotalResults > 0)
            {
                var firstResult = results.Results.Count > 1 ? results.Results.FirstOrDefault(r => r.Title.ToLower().Equals(title.ToLower())) : results.Results.FirstOrDefault();

                if(firstResult != null)
                {
                    DateOnly? releaseDate = null;

                    try
                    {
                        releaseDate = DateOnly
                        .Parse(firstResult.ReleaseDate);
                    }
                    catch (FormatException) { }

                    var posterPath = string.Empty;

                    if (!string.IsNullOrEmpty(firstResult.PosterPath))
                    {
                        posterPath = $"{imageUrl}/t/p/original{firstResult.PosterPath}";
                    }

                    ret = new Movie()
                    {
                        ImageUrl = posterPath,
                        Title = firstResult.Title,
                        ReleaseDate = releaseDate,
                        Description = firstResult.Overview,
                    };
                }
            }

            return ret;
        }

    }

    internal class ApiResults
    {
        [JsonPropertyName("total_results")]
        public int TotalResults { get; set; }

        public List<ApiResult> Results { get; set; } = new List<ApiResult>();
    }

    internal class ApiResult
    {
        public string Title { get; set; } = string.Empty;

        public string Overview { get; set; } = string.Empty;

        [JsonPropertyName("poster_path")]
        public string PosterPath { get; set; } = string.Empty;

        [JsonPropertyName("release_date")]
        public string ReleaseDate { get; set; } = string.Empty;
    }

    public class Movie
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateOnly? ReleaseDate { get; set; } = null;

        public string ImageUrl { get; set; } = string.Empty;
    }
}
