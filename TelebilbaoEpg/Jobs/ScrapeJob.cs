using HtmlAgilityPack;
using Quartz;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using TableSpans.HtmlAgilityPack;
using TelebilbaoEpg.Database.Models;
using TelebilbaoEpg.Database.Repository;
using Telebilbap_Epg.Services;
using static System.Net.Mime.MediaTypeNames;

namespace TelebilbaoEpg.Jobs
{
    public class ScrapeJob : IJob
    {
        private IConfiguration _configuration;
        private IBroadCastRepository _broadCastRepository;
        private IMovieService _movieService;

        public ScrapeJob(IConfiguration configuration, IBroadCastRepository broadCastRepository, IMovieService movieService)
        {
            _configuration = configuration;
            _broadCastRepository = broadCastRepository;
            _movieService = movieService;
        }

        private List<TimeBlock> GetTimeBlocks(HtmlNode programTable)
        {
            var ret = new List<TimeBlock>();

            var timeBlocks = programTable.SelectNodes("tbody/tr/td[1]");

            if (timeBlocks != null)
            {
                TimeOnly? previousTime = null;

                var index = 0;
                var blockIndex = 0;

                foreach (var node in timeBlocks)
                {
                    if (!ret.Any(b => b.RowIndex == index))
                    {

                        var text = node.InnerText;

                        var currentBlock = new TimeBlock()
                        {
                            RowIndex = index,
                            BlockIndex = blockIndex,
                        };

                        if (!string.IsNullOrEmpty(text))
                        {
                            TimeOnly? parsedValue = null;

                            try
                            {
                                var sanitizedtext = text.Replace("::", ":");
                                parsedValue = TimeOnly.Parse(sanitizedtext);
                            }
                            catch (FormatException)
                            {
                                var sections = text.Split('.');

                                if (sections.Length == 2)
                                {
                                    var hourSection = sections[0];

                                    var minuteSection = sections[1];

                                    if (!string.IsNullOrEmpty(hourSection) && !string.IsNullOrEmpty(minuteSection))
                                    {
                                        var hour = int.Parse(hourSection);

                                        var minute = int.Parse(minuteSection);

                                        parsedValue = new TimeOnly(hour, minute);
                                    }
                                }
                            }

                            if (parsedValue.HasValue)
                            {
                                currentBlock.From = parsedValue.Value;
                            }
                        }

                        var shouldAdd = !ret.Any(b => b.From > currentBlock.From);

                        if (!shouldAdd)
                        {
                            //start of day by blocks
                            var startDay = ret.First(b => b.BlockIndex == 0).From;

                            if (currentBlock.From < startDay)
                            {
                                shouldAdd = ret.Any(b => currentBlock.From < b.From);
                            }
                        }

                        shouldAdd = shouldAdd && currentBlock.From.HasValue;

                        if (shouldAdd)
                        {
                            ret.Add(currentBlock);

                            if (previousTime.HasValue)
                            {
                                var previousBlock = ret
                                    .OrderByDescending(b => b.RowIndex)
                                    .FirstOrDefault(b => b.From < currentBlock.From);

                                if (previousBlock != null)
                                {
                                    previousBlock.To = currentBlock.From.Value;
                                }
                            }

                            previousTime = currentBlock.From;
                            blockIndex++;
                        }
                    }

                    index++;
                }

                var firstBlock = ret.OrderBy(b => b.RowIndex)
                        .FirstOrDefault();

                var lastBlock = ret.OrderByDescending(b => b.RowIndex)
                        .FirstOrDefault();

                if (firstBlock != null && lastBlock != null && firstBlock.From.HasValue)
                {
                    lastBlock.To = firstBlock.From.Value;
                }
            }

            return ret;
        }


        public async Task Execute(IJobExecutionContext context)
        {
            var tableScrapeUrl = _configuration.GetValue<string>("TableScrapeUrl");
            HtmlWeb hw = new HtmlWeb();
            HtmlDocument doc = hw.Load(tableScrapeUrl);

            var tableSpanExtension = new TableSpansExtension();

            var programTable = tableSpanExtension.ProcessTable(doc.DocumentNode.SelectSingleNode("//table"));

            var timeBlocks = GetTimeBlocks(programTable);

            // week starts at monday
            var startOfWeek = DateTime.Now.Date.AddDays(-((int)DateTime.Now.DayOfWeek) + 1);

            var dayColumnStart = 2;
            var dayColumnEnd = dayColumnStart + 7;


            var parsedBroadCasts = new List<BroadCast>();

            var tableRows = programTable.SelectNodes($"tbody/tr");

            for (int dayIndex = dayColumnStart; dayIndex < dayColumnEnd; dayIndex++)
            {
                var programBlocks = programTable.SelectNodes($"tbody/tr/td[{dayIndex}]");

                if (programBlocks != null)
                {
                    var day = startOfWeek.AddDays(dayIndex - dayColumnStart);

                    //reset counter
                    var rowIndex = 0;

                    foreach (var programBlock in programBlocks)
                    {
                        var currentDay = day;
                        var columnIndex = dayIndex;
                        var beginIndex = rowIndex;
                        var rowSpan = 0;

                        var rowPathIndex = programBlock.XPath.IndexOf("/tr");
                        var xpath = $"//table/tbody{programBlock.XPath.Substring(rowPathIndex)}";
                        var originalNode = doc.DocumentNode.SelectSingleNode(xpath);

                        if (originalNode != null)
                        {
                            if (originalNode.Attributes.Contains("rowspan"))
                            {
                                rowSpan = int.Parse(originalNode.Attributes["rowspan"].Value);
                            }
                        }

                        var broadCastsToAdd = new List<BroadCast>();

                        TimeOnly? startTime = null;
                        TimeOnly? endTime = null;

                        var startBlock = timeBlocks.FirstOrDefault(b => b.RowIndex == beginIndex);

                        if (startBlock == null)
                        {
                            startBlock = timeBlocks.OrderByDescending(b => b.RowIndex)
                                .Where(b => b.RowIndex <= rowIndex + 1)
                                .FirstOrDefault();
                        }

                        if (startBlock != null)
                        {
                            startTime = startBlock.From;
                            endTime = startBlock.To;
                        }

                        if (startTime.HasValue && endTime.HasValue)
                        {
                            if (startTime.Value.Hour < 7 || endTime.Value.Hour < 7)
                            {
                                currentDay = currentDay.AddDays(1);
                            }

                            var startDate = currentDay.AddTicks(startTime.Value.Ticks);
                            var endDate = currentDay.AddTicks(endTime.Value.Ticks);

                            var text = HttpUtility.HtmlDecode(programBlock.InnerText);

                            string timepattern = "(?:2[0-3]|[01]?[0-9])[:.][0-5]?[0-9]";
                            var needsSplitByTimePattern = Regex.IsMatch(text, timepattern);

                            var separator = "—";
                            var needsSplitBySeparator = text.Contains(separator);

                            var needsSplitByHorizontalRow = programBlock.SelectSingleNode("hr") != null;

                            if (needsSplitByTimePattern)
                            {
                                var match = Regex.Match(text, timepattern);

                                if (match.Success)
                                {
                                    var firstProgramText = text.Substring(0, match.Index);

                                    var secondProgramText = text.Substring(match.Index + match.Length);

                                    var splitTime = TimeOnly.Parse(match.Value);
                                    var splitDate = currentDay.AddTicks(splitTime.Ticks);


                                    if (!string.IsNullOrEmpty(firstProgramText))
                                    {

                                        var firstProgram = new BroadCast()
                                        {
                                            From = startDate,
                                            To = splitDate,
                                            Name = SanitizeText(firstProgramText),
                                        };

                                        broadCastsToAdd.Add(firstProgram);
                                    }

                                    if (!string.IsNullOrEmpty(secondProgramText))
                                    {
                                        var secondProgram = new BroadCast()
                                        {
                                            From = splitDate,
                                            To = endDate,
                                            Name = SanitizeText(secondProgramText),
                                        };
                                        broadCastsToAdd.Add(secondProgram);
                                    }
                                }
                            }
                            else if (needsSplitBySeparator)
                            {
                                var separatorIndex = text.IndexOf(separator);

                                var endBlock = timeBlocks.FirstOrDefault(b => b.RowIndex == beginIndex + rowSpan);

                                if (endBlock == null)
                                {
                                    endBlock = timeBlocks
                                                .OrderByDescending(b => b.RowIndex)
                                                .Where(b => beginIndex + rowSpan > b.RowIndex)
                                                .FirstOrDefault();
                                }

                                if (endBlock != null && endBlock.To.HasValue)
                                {
                                    var blockStartDate = startDate;
                                    var blockEndtime = endBlock.To.Value;
                                    var blockEndDate = currentDay.AddTicks(blockEndtime.Ticks);

                                    var duration = blockEndDate - blockStartDate;

                                    var splitDate = rowSpan > 0 ? blockStartDate.AddMinutes((int)duration.TotalMinutes / rowSpan) : blockStartDate.AddMinutes((int)duration.Minutes / 2);

                                    var firstProgramText = string.Empty;

                                    var secondProgramText = string.Empty;

                                    if (separatorIndex > 0)
                                    {
                                        firstProgramText = text.Substring(0, separatorIndex);

                                        secondProgramText = text.Substring(separatorIndex);
                                    }
                                    else
                                    {
                                        secondProgramText = text.Replace(separator, "");
                                    }

                                    var firstProgramName = SanitizeText(firstProgramText);

                                    var secondProgramName = SanitizeText(secondProgramText);

                                    if (!string.IsNullOrEmpty(firstProgramName))
                                    {
                                        var firstProgram = new BroadCast()
                                        {
                                            From = startDate,
                                            To = splitDate,
                                            Name = firstProgramName,
                                        };

                                        broadCastsToAdd.Add(firstProgram);
                                    }

                                    if (!string.IsNullOrEmpty(secondProgramName) && splitDate <= endDate)
                                    {
                                        var secondProgram = new BroadCast()
                                        {
                                            From = splitDate,
                                            To = endDate,
                                            Name = secondProgramName,
                                        };

                                        broadCastsToAdd.Add(secondProgram);
                                    }
                                }
                            }
                            else if (needsSplitByHorizontalRow)
                            {
                                var textNodes = new List<HtmlNode>();

                                var nodeCollection = programBlock.SelectNodes("strong");

                                if (nodeCollection != null)
                                {
                                    textNodes.AddRange(nodeCollection.Where(n => !string.IsNullOrEmpty(n.InnerText)).ToList());
                                }


                                nodeCollection = programBlock.SelectNodes("p");

                                if (nodeCollection != null)
                                {
                                    textNodes.AddRange(nodeCollection.Where(n => !string.IsNullOrEmpty(n.InnerText)).ToList());
                                }

                                var nodeCount = textNodes.Count;

                                if (nodeCount > 0)
                                {
                                    var endBlock = timeBlocks.FirstOrDefault(b => b.RowIndex == beginIndex + rowSpan);

                                    if (endBlock == null)
                                    {
                                        endBlock = timeBlocks
                                                    .OrderByDescending(b => b.RowIndex)
                                                    .Where(b => beginIndex + rowSpan > b.RowIndex)
                                                    .FirstOrDefault();
                                    }

                                    if (endBlock != null && endBlock.To.HasValue)
                                    {
                                        var blockStartDate = startDate;
                                        var blockEndtime = endBlock.To.Value;
                                        var blockEndDate = currentDay.AddTicks(blockEndtime.Ticks);
                                        var duration = blockEndDate - blockStartDate;

                                        var itemDuration = duration.TotalMinutes / nodeCount;

                                        for (int i = 0; i < nodeCount; i++)
                                        {
                                            var nodeStartDate = blockStartDate.AddMinutes(i * itemDuration);
                                            var nodeEndDate = nodeStartDate.AddMinutes(itemDuration);

                                            var node = textNodes[i];
                                            var nodeText = node.InnerText;
                                            var currentText = SanitizeText(nodeText);

                                            var currentBroadcast = new BroadCast()
                                            {
                                                From = nodeStartDate,
                                                To = nodeEndDate,
                                                Name = currentText,
                                            };

                                            broadCastsToAdd.Add(currentBroadcast);
                                        }
                                    }
                                }
                            }
                            else
                            {

                                var name = SanitizeText(text);

                                var previousIndex = rowIndex - 1;

                                var broadCast = new BroadCast()
                                {
                                    From = startDate,
                                    To = endDate,
                                    Name = name,
                                };

                                broadCastsToAdd.Add(broadCast);
                            }

                            foreach (var item in broadCastsToAdd)
                            {
                                var add = !parsedBroadCasts.Any(b => b.To >= item.From && b.Name.Equals(item.Name)) && !parsedBroadCasts.Any(b => b.From == item.From && b.To == item.To);

                                if (add)
                                {
                                    parsedBroadCasts.Add(item);
                                }
                                else
                                {
                                    var broadCastToUpdate = parsedBroadCasts.FirstOrDefault(b => b.To >= item.From && b.Name.Equals(item.Name));

                                    if (broadCastToUpdate != null)
                                    {
                                        broadCastToUpdate.To = item.To;
                                    }
                                }
                            }
                        }

                        rowIndex++;
                    }
                }
            }

            var stationProgramInformationUrl = _configuration.GetValue<string>("StationProgramInformationUrl");
            doc = hw.Load(stationProgramInformationUrl);

            var parsedPrograms = new List<ProgramItem>();


            var titleNodeCollection = doc.DocumentNode.SelectNodes("//h2[contains(@class, 'programa_title')]");

            if (titleNodeCollection != null)
            {
                foreach (var titleNode in titleNodeCollection)
                {
                    var title = SanitizeText(titleNode.InnerText);
                    var description = string.Empty;
                    var imageUrl = string.Empty;

                    var programWrapper = titleNode.ParentNode.ParentNode.ParentNode;

                    var imageWrapper = programWrapper.SelectSingleNode("div[contains(@class, 'wpb_single_image')]");

                    if (imageWrapper != null)
                    {
                        var imagenode = imageWrapper.SelectSingleNode("figure/div/img");

                        if (imagenode != null)
                        {
                            var attributeName = "src";
                            imageUrl = imagenode.Attributes.Contains(attributeName) ? imagenode.Attributes[attributeName].Value : imageUrl;
                        }
                    }

                    var descriptionNode = programWrapper.SelectSingleNode("div[contains(@class, 'vc_row-o-content-bottom')]"); //vc_row-o-content-bottom


                    if (descriptionNode != null)
                    {
                        description = SanitizeText(descriptionNode.InnerText);
                    }

                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(description))
                    {
                        var program = new ProgramItem
                        {
                            Description = description,
                            Name = title,
                            ImageUrl = imageUrl,
                        };

                        parsedPrograms.Add(program);
                    }
                }
            }

            foreach(var broadcast in parsedBroadCasts)
            {
                var program = parsedPrograms.FirstOrDefault(p => p.Name == broadcast.Name);

                if(program != null)
                {
                    broadcast.Description = program.Description;
                    broadcast.ImageUrl = program.ImageUrl;
                }
            }

            var startSaveDate = parsedBroadCasts.Min(x => x.From);
            var endSaveDate = parsedBroadCasts.Max(x => x.To);

            var movieIndicator = "Cine.";
            var movies = parsedBroadCasts.Where(b => b.Name.Contains(movieIndicator))
                .ToList();

            foreach(var movie in movies)
            {
                string yearPattern = "(\\d{4})";

                var textWithoutIndicator = movie.Name.Replace(movieIndicator, string.Empty).Trim();

                var match = Regex.Match(textWithoutIndicator, yearPattern);
                int? year = null;

                if (match.Success)
                {
                    year = int.Parse(match.Value);
                }

                var title = textWithoutIndicator;

                if (year.HasValue)
                {
                    var yearIndex = textWithoutIndicator.IndexOf(year.Value.ToString());
                    title = textWithoutIndicator.Substring(0, yearIndex).Replace(".", "").Trim();
                }

                var foundMovie = await _movieService.GetMovie(title, year);

                if (foundMovie != null)
                {
                    movie.Name = foundMovie.Title;
                    movie.Description = foundMovie.Description;
                    movie.ImageUrl = foundMovie.ImageUrl;
                }
            }

            var savedBroadCasts = _broadCastRepository.GetBroadCasts(DateOnly.FromDateTime(startSaveDate), DateOnly.FromDateTime(endSaveDate));


            foreach (var broadcast in parsedBroadCasts)
            {
                var shouldSave = !savedBroadCasts.Any(b => b.From == broadcast.From && b.To == broadcast.To && b.Name == broadcast.Name);

                if (shouldSave)
                {
                    _broadCastRepository.Add(broadcast);
                }
            }


        }
        private string SanitizeText(string text)
        {
            var ret = string.Empty;

            if (!string.IsNullOrEmpty(text))
            {
                //proper lower and upper case fromatting
                ret = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower()).Trim();
                ret = ret.Replace("\n", " ").Replace("  ", " ");

                var separatorIndex = ret.IndexOf("—");

                if (separatorIndex > -1)
                {
                    ret = ret.Substring(0, separatorIndex).Trim();
                }
            }

            return ret;
        }

        public class TimeBlock
        {
            public TimeOnly? From { get; set; }

            public TimeOnly? To { get; set; }

            public int RowIndex { get; set; }

            public int BlockIndex { get; set; }
        }

        public class ProgramItem
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; }

            public string ImageUrl { get; set; }
        }
    }
}
