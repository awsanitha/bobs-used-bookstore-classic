using Amazon.Extensions.NETCore.Setup;
using Amazon.Rekognition;
using Amazon.S3;
using BobsBookstoreClassic.Data;
using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
using Bookstore.Domain;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;

namespace Bookstore.Web
{
    public static class DependencyInjectionSetup
    {
        public static void ConfigureServices(IServiceCollection services, IWebHostEnvironment env)
        {
            // Domain services
            services.AddScoped<IBookService, BookService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IReferenceDataService, ReferenceDataService>();
            services.AddScoped<IOfferService, OfferService>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IAddressService, AddressService>();
            services.AddScoped<IShoppingCartService, ShoppingCartService>();
            services.AddScoped<IImageResizeService, ImageResizeService>();

            // Database
            var connectionString = BookstoreConfiguration.GetConnectionString("BookstoreDatabaseConnection");
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            // Repositories
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IAddressRepository, AddressRepository>();
            services.AddScoped<IBookRepository, BookRepository>();
            services.AddScoped<IOfferRepository, OfferRepository>();
            services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

            // PaginatedList factory
            services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

            // File service
            if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
            {
                services.AddAWSService<IAmazonS3>();
                services.AddScoped<IFileService, S3FileService>();
            }
            else
            {
                var contentPath = Path.Combine(env.ContentRootPath, "Content");
                services.AddSingleton<IFileService>(new LocalFileService(contentPath));
            }

            // Image validation service
            if (BookstoreConfiguration.GetSetting("Services/ImageValidationService") == "aws")
            {
                services.AddAWSService<IAmazonRekognition>();
                services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
            }
            else
            {
                services.AddScoped<IImageValidationService, LocalImageValidationService>();
            }

            // Local auth middleware (registered so it can be resolved from DI)
            if (BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
            {
                services.AddScoped<LocalAuthenticationMiddleware>();
            }
        }
    }
}
