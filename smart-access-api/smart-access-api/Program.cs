using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using smart_access_api.Persistence;
using smart_access_api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


// Firestore: una sola instancia de FirebaseService (que arma el FirestoreDb) y
// un FirestoreContext por scope que lo envuelve y expone las colecciones.
builder.Services.AddSingleton<FirebaseService>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<FirebaseService>().FirestoreDb);
builder.Services.AddScoped<FirestoreContext>();

// AuthService: una instancia por scope que se encarga de validar el token JWT y extraer el UID del usuario.
builder.Services.AddScoped<AuthService>();

// LabNoteService: una instancia por scope para el CRUD de notas de laboratorio.
builder.Services.AddScoped<LabNoteService>();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    // Registramos el transformer que agrega el botón de Bearer en Scalar
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

// Autentiicaciones JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // Verificar que el token lo emitimos nosotros (app)
        ValidateIssuer = true,
        // Verificar que el token est para la misma app
        ValidateAudience = true,
        // Verificar que el token no ha expirado
        ValidateLifetime = true,
        // Verificar que la firma es valida
        ValidateIssuerSigningKey = true,
        // Verificar que estos valores coincidan con los que usamos para generar el token
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            )
    };
});

// AddAuthorization, habilitar el uso del [Authorize] en los controllers
builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
   options.AddPolicy("AllowAll", policy =>
   {
       policy.AllowAnyOrigin()
           .AllowAnyMethod()
           .AllowAnyHeader();
   }); 
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
