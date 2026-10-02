using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManager.Application.Auth;
using TaskManager.Application.Tasks;

namespace TaskManager.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
