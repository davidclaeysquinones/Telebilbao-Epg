# Telebilbao-Epg

Scraper with api for telebilbao epg

Epg information for this local tv station is available at https://www.telebilbao.es/programacion/
This program tries to parse the information on that page and to expose it with an api.

The intention is to integrate this API with [epg](https://github.com/iptv-org/epg) at some point.

## Endpoints

### /api/BroadCast/today

Description:
	Gets the schedule for today

### /api/BroadCast

Description:
	Gets the schedule between the specified dates
Parameters :
- from : start date to get the schedule
- to : end date to get the schedule

## Movie API

For movies the page does not contain any metada or poster.
In order to get this data [TMDB](https://developer.themoviedb.org/reference/intro/getting-started) is used.
In order to get your API key follow the steps on [this](https://developer.themoviedb.org/docs/getting-started) page.

## Docker image

### Environment Variables

| Variable                      | Description                                                            | Default                       |
|-------------------------------|------------------------------------------------------------------------|-------------------------------|
| JOB_SCHEDULE                  | Cron expression indicating the scraping recurrence                     | 0 0/30 * * * ?                |
| MOVIE_API_URL                 | The url to the movie API                                               | https://api.themoviedb.org/   |
| MOVIE_IMAGE_URL               | The base url for images on the movie API                               | https://image.tmdb.org        |    
| MOVIE_API_KEY                 | The API key for the API                                                | N/A                           |

### Compose file

```sh
version: '3.3'
services:
  epg:
    image: git.claeyscloud.com/david/telebilbao-epg:latest
	ports:
      - 6060:443
    environment:
      # specify the time zone for the server
      - TZ=Etc/UTC
	  - MOVIE_API_KEY=YOUR_KEY
    restart: unless-stopped
```