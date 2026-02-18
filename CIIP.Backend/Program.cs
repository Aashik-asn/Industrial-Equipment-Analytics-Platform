using CIIP.Backend.Data;
using CIIP.Backend.GraphQL.Mutations;
using CIIP.Backend.GraphQL.Queries;
using CIIP.Backend.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CiipDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));


builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddHostedService<TelemetryProcessor>();
builder.Services.AddScoped<PlantDashboardService>();
builder.Services.AddScoped<MachineDetailsService>();
builder.Services.AddScoped<ThresholdService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<PlantService>();
builder.Services.AddScoped<InfrastructureService>();


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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapGraphQL();

app.Run();
