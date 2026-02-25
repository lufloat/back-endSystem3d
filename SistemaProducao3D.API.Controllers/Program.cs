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
        policy
            // ✅ Mantém os origins explícitos que você já tinha (corrigindo o que estava errado)
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://127.0.0.1:5173", // ✅ corrigido (antes estava 127.0.0.5173)
                "https://front-end-system3-d.vercel.app"
            )
            // ✅ Adiciona suporte para qualquer deploy/preview da Vercel
            // (e mantém AllowCredentials funcionando)
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;

                // mantém os que já estão no WithOrigins
                if (origin == "http://localhost:5173") return true;
                if (origin == "http://localhost:3000") return true;
                if (origin == "http://127.0.0.1:3000") return true;
                if (origin == "http://127.0.0.1:5173") return true;
                if (origin == "https://front-end-system3-d.vercel.app") return true;

                // libera previews do Vercel: https://qualquer-coisa.vercel.app
                if (origin.StartsWith("https://") && origin.EndsWith(".vercel.app")) return true;

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
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

app.UseCors("AllowReact");

app.UseSwagger();
app.UseSwaggerUI();

var httpsPort = builder.Configuration["ASPNETCORE_HTTPS_PORT"]
    ?? builder.Configuration["HTTPS_PORT"];

if (!string.IsNullOrWhiteSpace(httpsPort))
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();

Console.WriteLine("========================================");
Console.WriteLine("API iniciada com sucesso!");
Console.WriteLine("CORS habilitado para: http://localhost:5173, http://localhost:3000, http://127.0.0.1:5173, http://127.0.0.1:3000 e *.vercel.app");
Console.WriteLine("Densidade sera buscada nas 6 impressoras Ultimaker");
Console.WriteLine("========================================");

app.Run();