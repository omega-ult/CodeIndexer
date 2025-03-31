using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 添加控制器
builder.Services.AddControllers().AddNewtonsoftJson();

// 添加Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CodeIndexer API", Version = "v1" });
});

// 添加CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// 配置HTTP请求管道
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeIndexer API v1"));
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

app.Run();