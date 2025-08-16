using FoodProviderAPI.Application.Contexts.RabbitMQ;
using FoodProviderAPI.Application.Services.RabbitMQ.Facade;
using FoodProviderAPI.Persistence.Contexts.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

#region RabbitMQ
builder.Services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
builder.Services.AddScoped<IRabbitMqChannelProvider, RabbitMqChannelProvider>();
builder.Services.AddScoped<IRabbitMqPublisher, RabbitMqPublisher>();
#endregion

#region Services
builder.Services.AddScoped<IRabbitMqFacada, RabbitMqFacada>();


#endregion


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();