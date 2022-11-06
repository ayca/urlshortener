using HashidsNet;
using LiteDB;
using urlshortener.api.models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ILiteDatabase, LiteDatabase>(_ =>
	new LiteDatabase("urlshortener"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(setup => setup.SwaggerDoc("v1",
	new Microsoft.OpenApi.Models.OpenApiInfo
	{
		Description = "Url shortning service",
		Title = "Url Shortener",
		Version = "v1"
	}));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

Hashids hashids = new Hashids("Url Shortener", 6);

app.MapPost("/add", (UrlInfo urlInfo, ILiteDatabase dbContext) =>
{
	if (urlInfo is null || string.IsNullOrWhiteSpace(urlInfo.url))
	{
		return Results.BadRequest("url to shorten must be provided");
	}

	ILiteCollection<UrlInfo> collection = dbContext.GetCollection<UrlInfo>(BsonAutoId.Int32);
	UrlInfo dbEntry = collection.Query()
		.Where(x => x.url.Equals(urlInfo.url))
		.FirstOrDefault();

	if (dbEntry is not null)
	{
		if (!string.IsNullOrWhiteSpace(urlInfo.shortUrl) && !urlInfo.shortUrl.Equals(dbEntry.shortUrl))
		{
			return Results.Conflict("given short url is not available");
		}

		return Results.Ok(dbEntry.shortUrl);
	}

	BsonValue documentId = collection.Insert(urlInfo);
	string shortUrl = !string.IsNullOrWhiteSpace(urlInfo.shortUrl)
		? urlInfo.shortUrl
		: hashids.Encode(documentId);

	urlInfo.id = documentId;
	urlInfo.shortUrl = shortUrl;
	collection.Update(urlInfo);

	return Results.Created(shortUrl, shortUrl);
})
.Produces<string>(StatusCodes.Status200OK)
.Produces<string>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status409Conflict)
;

app.MapGet("/{shortUrl}", (string shortUrl, ILiteDatabase dbContext) =>
{
	ILiteCollection<UrlInfo> collection = dbContext.GetCollection<UrlInfo>();

	var urlInfo = collection.Query()
		.Where(x => x.shortUrl.Equals(shortUrl))
		.FirstOrDefault();

	if (urlInfo is not null)
	{
		return Results.Redirect(urlInfo.url, permanent: true);
	}

	return Results.NotFound();
})
.Produces(StatusCodes.Status308PermanentRedirect)
.Produces(StatusCodes.Status404NotFound)
;

app.Run();
