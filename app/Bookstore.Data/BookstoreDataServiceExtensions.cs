using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookstore.Data
{
    public static class BookstoreDataServiceExtensions
    {
        public static IServiceCollection AddBookstoreDbContext(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            return services;
        }
    }
}
