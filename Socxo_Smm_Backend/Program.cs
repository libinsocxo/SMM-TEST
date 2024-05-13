using MongoDB.Driver;
using Socxo_Smm_Backend.Infrastructure.Socxo_Smm_Backend.Infrastructure.Repository.Interface;
using Socxo_Smm_Backend.Infrastructure.Socxo_Smm_Backend.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


//MongoDb Connection
builder.Services.AddSingleton<IMongoClient>(new MongoClient(builder.Configuration.GetConnectionString("MongoDBConnection")));


//Adding Services

builder.Services.AddScoped <ILinkedIn,LinkedIn> ();
//builder.Services.AddScoped<IAuthDao, AuthDao>();
//builder.Services.AddScoped<IRoomDao, RoomDao>();

// Adding HttpClient

builder.Services.AddHttpClient();

// cors policy
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("AllowSpecificOrigin",
        builder =>
        {
            builder.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
});


var app = builder.Build();

app.UseCors("AllowSpecificOrigin");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
