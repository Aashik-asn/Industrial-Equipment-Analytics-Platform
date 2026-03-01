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
// CORS   ADD THIS BLOCK (VERY IMPORTANT)
// ======================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174", 
                "http://localhost:5175",
                "http://localhost:5176",
                "http://localhost:5177",
                "http://localhost:50429",
                "http://localhost:5178",
                "http://localhost:5188"
            ) // React dev server ports
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

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

builder.Services.AddHostedService<TelemetryProcessor>();

// ======================================================
// GRAPHQL
// ======================================================
builder.Services
    .AddGraphQLServer()
    .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = true)
    .AddAuthorization()
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
    .AddTypeExtension<InfrastructureQuery>()
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
// IMPORTANT ORDER � AUTH + CORS BEFORE GRAPHQL
// ======================================================
app.UseRouting();

app.UseCors("AllowFrontend");   // NOW THIS WILL WORK

app.UseAuthentication();
app.UseAuthorization();

// GraphQL Endpoint
app.MapGraphQL();

app.Run();