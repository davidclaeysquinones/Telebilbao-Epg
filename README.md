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

## Pending

Sometimes movies are emitted on this channel.
The titles are mentioned in Spanish together with the release year.
First the IMDB API was considered to acomplish this but it might not work with Spanish retro titles.
Furthermore the IMDB requires an API token which would make the scraper more difficult to use.
An alternative that supports Spanish and without API tokens needs to be found.