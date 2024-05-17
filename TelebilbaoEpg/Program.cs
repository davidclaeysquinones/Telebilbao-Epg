using Quartz;
using TelebilbaoEpg.Database.Repository;
using TelebilbaoEpg.Jobs;
using Telebilbap_Epg.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();

builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService(opt =>
{
    opt.WaitForJobsToComplete = true;
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddScoped<IMovieService, MovieService>();
builder.Services.AddScoped<IBroadCastRepository, BroadCastRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

var configuration = app.Configuration;

string jobSchedule = configuration.GetValue<string>("Quartz:JobSchedule");

var schedulerFactory = app.Services.GetRequiredService<ISchedulerFactory>();
var scheduler = await schedulerFactory.GetScheduler();

// define the job and tie it to our HelloJob class
var job = JobBuilder.Create<ScrapeJob>()
    .Build();

var trigger = TriggerBuilder.Create()
    .WithIdentity("Cron trigger", "Scrape")
    .StartNow()
    .WithCronSchedule(jobSchedule)
    .Build();

//var trigger = TriggerBuilder.Create()
//    .WithIdentity("Cron trigger", "Scrape")
//    .StartNow()
//    .Build();

await scheduler.ScheduleJob(job, trigger);

app.Run();
