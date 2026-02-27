using Business_Logic.Repositories;
using Business_Logic.Repositories.Interfaces;
using Business_Logic.Services;
using Business_Logic.Serviços;
using Business_Logic.Serviços.Interfaces;
using Business_Logic.Serviços.Sync;
using Microsoft.EntityFrameworkCore;
using SistemaProducao3D.Data.Context;
using SistemaProducao3D.Integration.Ultimaker;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// CORS
// ========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "http://localhost:3000",
            "http://127.0.0.1:3000",
            "https://front-end-system3-d.vercel.app",
            "http://system3dback-frontend-7u5oui-5bc0d1-189-112-233-141.traefik.me",
            "https://system3dback-frontend-7u5oui-5bc0d1-189-112-233-141.traefik.me",
            "http://192.168.148.19:8089"
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

// ========================================
// CONTROLADORES E SWAGGER
// ========================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ========================================
// BANCO DE DADOS
// ========================================
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ========================================
// HTTPCLIENT
// ========================================
builder.Services.AddHttpClient<UltimakerApiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<IUltimakerClient, UltimakerClient>();

// ========================================
// CONFIGURAÇÕES
// ========================================
builder.Services.Configure<UltimakerOptions>(
    builder.Configuration.GetSection("Ultimaker"));

// ========================================
// REPOSITÓRIOS
// ========================================
builder.Services.AddScoped<IMaterialRepository, MaterialRepository>();
builder.Services.AddScoped<IProducaoRepository, ProducaoRepository>();

// ========================================
// SERVIÇOS
// ========================================
builder.Services.AddMemoryCache();
builder.Services.AddScoped<UltimakerApiService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IDashboardSKUService, DashboardSKUService>();
builder.Services.AddScoped<IVisaoGeralService, VisaoGeralService>();
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddScoped<ICalculoService, CalculoService>();
builder.Services.AddScoped<IProducaoService, ProducaoService>();
builder.Services.AddScoped<IEquipamentoService, EquipamentoService>();
builder.Services.AddScoped<IProdutoEspecificoService, ProdutoEspecificoService>();
builder.Services.AddScoped<ITimelineService, TimelineService>();

var app = builder.Build();

// ========================================
// PIPELINE
// ========================================
app.UseRouting();
app.UseCors("AllowReact");   // ← deve vir ANTES do UseAuthorization
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("========================================");
Console.WriteLine("API iniciada com sucesso!");
Console.WriteLine("CORS habilitado para origens configuradas");
Console.WriteLine("Densidade sera buscada nas 6 impressoras Ultimaker");
Console.WriteLine("========================================");

app.Run();