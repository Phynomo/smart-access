using Scalar.AspNetCore;
using smart_access_api.Persistence;
using smart_access_api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Firestore: una sola instancia de FirebaseService (que arma el FirestoreDb) y
// un FirestoreContext por scope que lo envuelve y expone las colecciones.
builder.Services.AddSingleton<FirebaseService>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<FirebaseService>().FirestoreDb);
builder.Services.AddScoped<FirestoreContext>();

builder.Services.AddScoped<AuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthorization();
app.MapControllers();
app.Run();
