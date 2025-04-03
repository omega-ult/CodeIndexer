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

// 创建wwwroot目录（如果不存在）
var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
if (!Directory.Exists(wwwrootPath))
{
    Directory.CreateDirectory(wwwrootPath);
    // 创建默认的index.html文件
    var indexHtmlPath = Path.Combine(wwwrootPath, "index.html");
    File.WriteAllText(indexHtmlPath, @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>CodeIndexer服务</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; line-height: 1.6; }
        .container { max-width: 800px; margin: 0 auto; padding: 20px; }
        h1 { color: #333; }
        .card { background: #f9f9f9; border-radius: 5px; padding: 15px; margin-bottom: 20px; }
        .api-link { display: block; margin: 10px 0; }
    </style>
</head>
<body>
    <div class=""container"">
        <h1>CodeIndexer API服务</h1>
        <div class=""card"">
            <h2>欢迎使用CodeIndexer</h2>
            <p>这是CodeIndexer的API服务端。您可以通过以下链接访问API文档：</p>
            <a class=""api-link"" href=""/swagger"">Swagger API文档</a>
        </div>
        <div class=""card"">
            <h2>可用API端点</h2>
            <ul>
                <li><code>POST /api/codeindex/index</code> - 索引代码目录</li>
                <li><code>POST /api/codeindex/index/dll</code> - 索引DLL文件目录</li>
                <li><code>GET /api/codeindex/search/name/{pattern}</code> - 按名称模糊查询</li>
                <li><code>GET /api/codeindex/search/fullname/{fullName}</code> - 按全名精确查询</li>
                <li><code>GET /api/codeindex/search/type/{elementType}</code> - 按元素类型查询</li>
                <li><code>GET /api/codeindex/search/parent/{parentId}</code> - 按父元素查询子元素</li>
            </ul>
        </div>
    </div>
</body>
</html>");
}

// 启用静态文件
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

// 添加默认文件支持
app.MapFallbackToFile("index.html");

app.Run();