using RemotePhone.Database;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using RemotePhone.Services;
using RemotePhone.Hubs;
using Microsoft.EntityFrameworkCore.Sqlite;
System.IO.Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

string connecttext = "Data Source=" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Phone.db");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connecttext));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
// Add services to the container.

builder.Services.AddSignalR(options =>
{
    //要求30s内必须收到客户端发的一条消息，如果没有收到，那么服务器端则认为客户端掉了
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 2048_000;
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;//解决后端传到前端全大写
    options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);//解决后端返回数据中文被编码
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy =>
    {
         policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddSingleton<ILoggerService, LoggerService>();
builder.Services.AddSingleton<IStartUpService, StartUpService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHub<ImageHub>("/ImageHub");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    //context.Database.EnsureCreated();
    if (context.Database.GetPendingMigrations().Count() > 0)
    {
        context.Database.Migrate();
    }
    var startUp = scope.ServiceProvider.GetRequiredService<IStartUpService>();
    startUp.StartScan();
}

app.Run();
