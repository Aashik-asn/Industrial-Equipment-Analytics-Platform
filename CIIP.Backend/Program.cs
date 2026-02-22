using CIIP.Backend.Data;
using CIIP.Backend.GraphQL.Mutations;
using CIIP.Backend.GraphQL.Queries;
using CIIP.Backend.Services;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;




var builder = WebApplication.CreateBuilder(args);

// ======================================================
// DATABASE
// ======================================================
builder.Services.AddDbContext<CiipDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// ======================================================
// SERVICES
// ======================================================
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<PlantDashboardService>();
builder.Services.AddScoped<MachineDetailsService>();
builder.Services.AddScoped<ThresholdService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<PlantService>();
builder.Services.AddScoped<InfrastructureService>();
builder.Services.AddScoped<JwtService>();

// Background Telemetry Processor
builder.Services.AddHostedService<TelemetryProcessor>();

// ======================================================
// GRAPHQL
// ======================================================
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()

    .AddTypeExtension<AuthMutation>()
    .AddTypeExtension<ProfileMutation>()
    .AddTypeExtension<AlertMutation>()
    .AddTypeExtension<ThresholdMutation>()
    .AddTypeExtension<PlantMutation>()
    .AddTypeExtension<InfrastructureMutation>()

    .AddTypeExtension<ProfileQuery>()
    .AddTypeExtension<DashboardQuery>()
    .AddTypeExtension<PlantDashboardQuery>()
    .AddTypeExtension<MachineDetailsQuery>()
    .AddTypeExtension<AlertQuery>()
    .AddTypeExtension<ThresholdQuery>()
    .AddFiltering()
    .AddSorting()
    .AddProjections();

// ======================================================
// JWT AUTHENTICATION
// ======================================================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    var key = builder.Configuration["Jwt:Key"];

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key!)
        )
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// ======================================================
// DEVELOPMENT SETTINGS
// ======================================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// ======================================================
// IMPORTANT ORDER — AUTH BEFORE GRAPHQL
// ======================================================
app.UseAuthentication();
app.UseAuthorization();

// GraphQL Endpoint
app.MapGraphQL();

app.Run();
